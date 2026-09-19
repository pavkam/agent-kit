// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

using System.Collections.Immutable;

/// <summary>A session-backed <see cref="IInputQueue"/> that persists admission and promotion through <see cref="ISessionCoordinator"/>.</summary>
/// <remarks>
/// <para>
/// This is the first-party implementation of the durable, idempotent admission and atomic promotion contract
/// <see cref="IInputQueue"/> defines. It owns no state of its own: every admitted or promoted fact is committed
/// through the selected session store's lane protocol
/// (<see cref="ISessionCoordinator.AdmitInputAsync(SessionInputAdmissionRequest, SessionExecutionCapability, CancellationToken)"/>,
/// <see cref="ISessionCoordinator.LoadLaneStateAsync(SessionLaneStateRequest, SessionExecutionCapability, CancellationToken)"/>,
/// <see cref="ISessionCoordinator.LoadPendingInputsAsync(SessionPendingInputsRequest, SessionExecutionCapability, CancellationToken)"/>,
/// and <see cref="ISessionCoordinator.PromoteInputAsync(SessionInputPromotionRequest, SessionExecutionCapability, CancellationToken)"/>),
/// so admission and history can never diverge.
/// </para>
/// <para>
/// The instance is bound to one compiled <see cref="SessionExecutionCapability"/> and is therefore run-scoped: a
/// host composes one instance per run, matching how the session coordinator and profile it wraps are themselves
/// selected per run. It must not be shared as a process-wide singleton across unrelated runs or sessions.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Queue is the normative domain name for durable input admission and promotion.")]
public sealed class SessionBackedInputQueue: IInputQueue
{
    private readonly ISessionCoordinator _session;
    private readonly SessionExecutionCapability _capability;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly IIdentifierGenerator<MessageId> _messageIds;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumPendingInputsPerLane;

    /// <summary>Initializes a session-backed queue bound to one run's compiled session execution capability.</summary>
    /// <param name="session">The non-null session coordinator this queue persists admission and promotion through.</param>
    /// <param name="capability">The non-null compiled session profile, coordinator selection, and run-coordinator binding for this run.</param>
    /// <param name="entryIds">The non-null allocator for new session-entry identities.</param>
    /// <param name="messageIds">The non-null allocator for new message identities.</param>
    /// <param name="timeProvider">The non-null injected clock used for promotion commit timestamps.</param>
    /// <param name="options">The optional bound queue bounds, or <see langword="null"/> to use this package's documented defaults.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    public SessionBackedInputQueue(
        ISessionCoordinator session,
        SessionExecutionCapability capability,
        IIdentifierGenerator<SessionEntryId> entryIds,
        IIdentifierGenerator<MessageId> messageIds,
        TimeProvider timeProvider,
        IOptions<AgentIOOptions>? options = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(messageIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _session = session;
        _capability = capability;
        _entryIds = entryIds;
        _messageIds = messageIds;
        _timeProvider = timeProvider;
        _maximumPendingInputsPerLane = options?.Value.MaximumPendingInputsPerLane ?? new AgentIOOptions().MaximumPendingInputsPerLane;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <paramref name="request"/>'s <see cref="InputAdmissionRequest.Correlation"/> must be a
    /// <see cref="BeforeRunOperationCorrelation"/>: admission is always its own independent operation at the
    /// session-store boundary, whether or not the caller happens to be currently driving an active run on the same
    /// lane. A caller submitting mid-run steering input mints a fresh before-run correlation and captures fresh
    /// authorization for it before calling <see cref="IInputCoordinator.AdmitAsync"/>; it does not reuse its own
    /// in-run correlation here.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="request"/>'s correlation is not before-run.</exception>
    public async ValueTask<InputAdmissionResult> AppendAsync(
        InputAdmissionRequest request,
        AdmissionId admissionId,
        AgentInput effectiveInput,
        InputPreprocessingManifest preprocessing,
        DateTimeOffset admittedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = new SessionOperationContext(
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.Correlation, request.Identity, request.Authorization);
        var loaded = await _session.LoadAsync(context, _capability.Profile, cancellationToken).ConfigureAwait(false);
        if (loaded is not SessionLoaded session)
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.AddressNotFound, "The addressed session is unavailable."));
        }

        var laneState = await _session.LoadLaneStateAsync(
            new SessionLaneStateRequest(context), _capability, cancellationToken).ConfigureAwait(false);
        if (laneState is not SessionLaneStateLoaded lane)
        {
            return new RejectedInput(new InputRejection(InputRejectionKind.AddressNotFound, "The execution lane has not been provisioned."));
        }

        var admission = new SessionInputAdmissionRequest(
            context, admissionId, _entryIds.Create(), request.Input, effectiveInput, preprocessing, admittedAt,
            request.ExpectedVersion ?? session.Descriptor.Version, lane.State.Revision, lane.State.BranchCursor,
            new IdempotencyKey($"agentkit.io.queue.admit:{admissionId}"), _maximumPendingInputsPerLane);
        return await _session.AdmitInputAsync(admission, _capability, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public async ValueTask<InputPromotionResult> PromoteAsync(
        InputPromotionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = new SessionOperationContext(
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.ExpectedOperation, request.Identity,
            request.Authorization);
        var pendingResult = await _session.LoadPendingInputsAsync(
            new SessionPendingInputsRequest(context), _capability, cancellationToken).ConfigureAwait(false);
        if (pendingResult is not SessionPendingInputsLoaded pending)
        {
            return new InputPromotionRejected(new InputRejection(
                InputRejectionKind.AddressNotFound, "The execution lane's pending input could not be discovered."));
        }

        var eligible = pending.Pending
            .Where(input => input.AdmittedSequence.Value <= request.CutoffSequence.Value)
            .Take(request.MaximumPromotions)
            .ToImmutableArray();
        if (eligible.Length == 0)
        {
            return new InputPromotionRejected(new InputRejection(
                InputRejectionKind.NoEligibleInput, "No durably admitted input is eligible for this promotion boundary."));
        }

        SessionVersion expectedVersion;
        if (request.ExpectedVersion is { } suppliedVersion)
        {
            expectedVersion = suppliedVersion;
        }
        else
        {
            var loaded = await _session.LoadAsync(context, _capability.Profile, cancellationToken).ConfigureAwait(false);
            if (loaded is not SessionLoaded session)
            {
                return new InputPromotionRejected(new InputRejection(
                    InputRejectionKind.AddressNotFound, "The addressed session is unavailable."));
            }

            expectedVersion = session.Descriptor.Version;
        }

        var admissionIds = eligible.Select(static input => input.AdmissionId).ToImmutableArray();
        var entryIds = eligible.Select(_ => _entryIds.Create()).ToImmutableArray();
        var messageIds = eligible.Select(_ => _messageIds.Create()).ToImmutableArray();
        var promotionEntryId = _entryIds.Create();
        var promote = new SessionInputPromotionRequest(
            context, admissionIds, request.CutoffSequence, pending.Revision, expectedVersion,
            pending.BranchCursor, promotionEntryId, entryIds, messageIds, request.OperationStateRevision,
            _timeProvider.GetUtcNow(), new IdempotencyKey($"agentkit.io.queue.promote:{promotionEntryId}"));
        var result = await _session.PromoteInputAsync(promote, _capability, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            SessionInputPromoted committed => new InputPromoted(
                new InputPromotionSnapshot(
                    request.AgentId, request.SessionId, request.ExecutionLaneId, request.ExpectedOperation,
                    request.OperationStateRevision, request.BranchCursor, request.CutoffSequence,
                    request.ExpectedVersion, request.ExpectedFencingToken, request.Boundary, request.PreviousTurnId,
                    request.TargetTurnId, admissionIds),
                committed.Promoted,
                committed.SessionVersion),
            SessionInputPromotionRejected rejected => new InputPromotionRejected(
                new InputRejection(InputRejectionKind.StaleVersion, rejected.SafeReason)),
            _ => new InputPromotionRejected(new InputRejection(
                InputRejectionKind.StaleVersion, "The mid-run promotion could not be committed.")),
        };
    }
}
