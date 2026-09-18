// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>
/// An <see cref="IAgentLoop"/> that records each request, signals entry, optionally waits on a gate, and completes
/// with an assistant message so concurrency and observer behaviour can be exercised deterministically.
/// </summary>
internal sealed class GatedAgentLoop: IAgentLoop
{
    private readonly Lock _gate = new();

    /// <summary>Gets every request received, in order.</summary>
    public List<AgentRunRequest> Requests { get; } = [];

    /// <summary>Gets every services bundle received, in order.</summary>
    public List<AgentRunServices> Services { get; } = [];

    /// <summary>Gets or sets the gate every run awaits after signalling entry, or <see langword="null"/> to complete immediately.</summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal completed when the first run enters.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the highest number of runs that were inside the loop at the same time.</summary>
    public int PeakConcurrency { get; private set; }

    private int _active;

    /// <inheritdoc/>
    public async Task<AgentLoopResult> RunAsync(AgentRunRequest request, AgentRunServices services, CancellationToken cancellationToken = default)
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
            return new AgentLoopResult(request.AgentId, request.SessionId, request.BranchId, request.RunId, new AgentRunCompleted(assistant), [assistant], new SessionVersion(1));
        }
        finally
        {
            lock (_gate)
            {
                _active--;
            }
        }
    }
}
