// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using AgentKit.TestSupport;

/// <summary>
/// An <see cref="IAgentLoop"/> that records each request, signals entry, optionally waits on a gate, and completes
/// with an assistant message so concurrency and observer behaviour can be exercised deterministically.
/// </summary>
internal sealed class GatedAgentLoop: IAgentLoop
{
    private readonly Lock _gate = new();

    /// <summary>Gets every request received, in order.</summary>
    public List<AgentLoopRunRequest> Requests { get; } = [];

    /// <summary>Gets every services bundle received, in order.</summary>
    public List<AgentRunServices> Services { get; } = [];

    /// <summary>Gets or sets the gate every run awaits after signalling entry, or <see langword="null"/> to complete immediately.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal completed when the first run enters.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the highest number of runs that were inside the loop at the same time.</summary>
    public int PeakConcurrency { get; private set; }

    /// <summary>Gets or sets a factory that replaces the completed outcome, for settlement tests.</summary>
    public Func<AgentLoopRunRequest, AgentRunOutcome>? OutcomeOverride { get; init; }

    /// <summary>
    /// When <see langword="true"/>, polls durable abort through the session coordinator after the gate releases,
    /// mirroring <c>DefaultAgentLoop</c> cancel boundaries for facade tests.
    /// </summary>
    public bool HonorDurableAbort { get; init; }

    private int _active;

    /// <inheritdoc/>
    public async Task<AgentLoopResult> RunAsync(AgentLoopRunRequest request, AgentRunServices services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(services);
        lock (_gate)
        {
            Requests.Add(request);
            Services.Add(services);
            _active++;
            PeakConcurrency = Math.Max(PeakConcurrency, _active);
        }

        _ = Entered.TrySetResult();
        try
        {
            if (request.Observer is { } observer)
            {
                await observer.OnEventAsync(
                    new AgentRunModelResponseEvent(new TurnId(Guid.NewGuid()), new ModelPartDelta(new ModelRequestId(Guid.NewGuid()), 1, 0, new TextContentDelta("hi"))),
                    cancellationToken);
            }

            if (Gate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (HonorDurableAbort
                && request.LaneAdmission is { } admission
                && await IsDurableAbortRequestedAsync(request, services, admission, cancellationToken).ConfigureAwait(false))
            {
                var abortedVersion = await CurrentVersionAsync(request, services, cancellationToken).ConfigureAwait(false);
                return new AgentLoopResult(
                    request.AgentId, request.SessionId, request.BranchId, request.RunId,
                    new RunCancelled(new CancellationReason(RunResultTestData.Error(AgentErrorCodes.Cancelled))),
                    [], abortedVersion, null, new RunUsage(request.RunId, []), new RunSettlementCompleted());
            }

            var assistant = new AssistantMessage(
                new MessageId(Guid.NewGuid()), request.AgentId, request.SessionId, null, request.BranchId, request.RunId, null,
                DateTimeOffset.UnixEpoch, MessageState.Complete,
                [new TextPart("ok", TextSemantics.Plain, ExtensionData.Empty)],
                new AssistantResponseMetadata(
                    new ModelRequestId(Guid.NewGuid()),
                    new ProviderResponseIdentity(new ProviderId("test"), null, new ApiFamilyId("test"), new ModelId("m"), new ModelId("m"), null, null, null),
                    NormalizedStopReason.Completed,
                    null,
                    ModelUsage.NotReported,
                    ExtensionData.Empty),
                ExtensionData.Empty);
            var outcome = OutcomeOverride?.Invoke(request) ?? new RunSucceeded();
            var finalVersion = await CurrentVersionAsync(request, services, cancellationToken).ConfigureAwait(false);
            return new AgentLoopResult(
                request.AgentId, request.SessionId, request.BranchId, request.RunId, outcome,
                outcome is RunSucceeded ? [assistant] : [], finalVersion, null,
                new RunUsage(request.RunId, []), new RunSettlementCompleted());
        }
        finally
        {
            lock (_gate)
            {
                _active--;
            }
        }
    }

    /// <summary>
    /// Reads the session's actual current version, since this fake commits nothing itself: the real admission
    /// path may have already advanced the version through provisioning, admission, and acceptance before this
    /// loop was ever entered, so a fixed version would be stale and fail a caller's later lane release.
    /// </summary>
    private static async ValueTask<bool> IsDurableAbortRequestedAsync(
        AgentLoopRunRequest request,
        AgentRunServices services,
        LoopLaneAdmission admission,
        CancellationToken cancellationToken)
    {
        var context = new SessionOperationContext(
            request.AgentId,
            request.SessionId,
            admission.ExecutionLaneId,
            admission.AcceptedCorrelation,
            request.Identity,
            request.Authorization);
        var capability = new SessionExecutionCapability(
            request.SessionProfile,
            services.Session,
            services.RunCoordinator!);
        var loaded = await services.Session.LoadRunStateAsync(new SessionRunStateRequest(context), capability, cancellationToken)
            .ConfigureAwait(false);
        return loaded is SessionRunStateLoaded { AbortRequested: true };
    }

    private static async Task<SessionVersion?> CurrentVersionAsync(
        AgentLoopRunRequest request, AgentRunServices services, CancellationToken cancellationToken)
    {
        var context = new SessionOperationContext(
            request.AgentId, request.SessionId, executionLaneId: null, request.Authorization.Scope.Correlation,
            request.Identity, request.Authorization);
        var page = await services.Session.ReadAsync(
            new SessionReadRequest(context, request.BranchId, new SessionSequence(0), pageSize: 1),
            request.SessionProfile, cancellationToken).ConfigureAwait(false);
        return page is SessionPage { Snapshot: { } snapshot } ? snapshot.Version : null;
    }
}
