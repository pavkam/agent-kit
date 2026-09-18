// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

using System.Collections.Immutable;

/// <summary>
/// A minimal stateful <see cref="ISessionCoordinator"/> double: creates sessions with fresh identities, loads them by
/// address, reports an exact single-branch snapshot, appends contiguous entries, and supports the full lane
/// admission protocol (provisioning, discovery, input admission, run acceptance, and release) so the engine's
/// admission boundary can drive a run end to end without a store adapter. Branching and deletion are unsupported.
/// </summary>
/// <remarks>
/// Every member records its request so tests can assert on identities, idempotency keys, and ordering. Access is
/// serialized with a lock, so concurrent turns on different sessions can share one instance.
/// </remarks>
public sealed class InMemoryTestSessionCoordinator: ISessionCoordinator
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SessionId, StoredSession> _sessions = [];

    /// <summary>Gets every create request received, in order.</summary>
    public List<SessionCreateRequest> CreateRequests { get; } = [];

    /// <summary>Gets every load context received, in order.</summary>
    public List<SessionOperationContext> LoadContexts { get; } = [];

    /// <summary>Gets every append request received, in order.</summary>
    public List<SessionAppendRequest> AppendRequests { get; } = [];

    /// <summary>Gets or sets a result that replaces the next create, or <see langword="null"/> to create normally.</summary>
    public SessionCreateResult? NextCreateResult { get; set; }

    /// <summary>Gets or sets a result that replaces every append, or <see langword="null"/> to append normally.</summary>
    public SessionAppendResult? AppendOverride { get; set; }

    /// <summary>Gets or sets a result that replaces the next run acceptance, or <see langword="null"/> to accept normally.</summary>
    public SessionRunStartResult? AcceptRunOverride { get; set; }

    /// <summary>Gets or sets a gate every append awaits before committing, for concurrency tests.</summary>
    public TaskCompletionSource? AppendGate { get; set; }

    /// <summary>Gets or sets a gate every run acceptance awaits before committing, for concurrency tests.</summary>
    public TaskCompletionSource? AcceptRunGate { get; set; }

    /// <summary>Seeds an existing session the coordinator will load.</summary>
    /// <param name="descriptor">The session's descriptor.</param>
    public void Seed(SessionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (_gate)
        {
            _sessions[descriptor.Address.SessionId] = new StoredSession(descriptor);
        }
    }

    /// <summary>Returns the entries committed to a session, in sequence order.</summary>
    /// <param name="sessionId">The session to inspect.</param>
    /// <returns>The committed entries, or empty when the session is unknown.</returns>
    public ImmutableArray<SessionEntry> EntriesOf(SessionId sessionId)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(sessionId, out var session) ? [.. session.Entries] : [];
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            CreateRequests.Add(request);
            if (NextCreateResult is { } scripted)
            {
                NextCreateResult = null;
                return ValueTask.FromResult(scripted);
            }

            var descriptor = new SessionDescriptor(
                new SessionAddress(request.AgentId, new SessionId(Guid.NewGuid())),
                request.ConversationId,
                request.Identity.TenantId,
                request.Identity.PrincipalId,
                profile.DefaultStoreKey,
                new BranchId(Guid.NewGuid()),
                new SessionVersion(0),
                SessionLifecycleState.Active,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch,
                new SchemaVersion("1"),
                ExtensionData.Empty);
            _sessions[descriptor.Address.SessionId] = new StoredSession(descriptor);
            return ValueTask.FromResult<SessionCreateResult>(new SessionCreated(descriptor, existing: false));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            LoadContexts.Add(context);
            return ValueTask.FromResult<SessionLoadResult>(
                _sessions.TryGetValue(context.SessionId, out var session)
                    ? new SessionLoaded(session.Descriptor with { Version = session.Version })
                    : new SessionNotFound(context.ToAddress()));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (AppendGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        lock (_gate)
        {
            AppendRequests.Add(request);
            if (AppendOverride is { } scripted)
            {
                return scripted;
            }

            if (!_sessions.TryGetValue(request.Context.SessionId, out var session))
            {
                return new SessionAppendNotFound(request.Context.ToAddress());
            }

            if (request.ExpectedVersion != session.Version)
            {
                return new SessionAppendConflict(request.ExpectedVersion, session.Version);
            }

            session.Entries.AddRange(request.Entries);
            session.Version = new SessionVersion(session.Version.Value + 1);
            return new SessionAppended(session.Version, request.Entries);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(request.Context.SessionId, out var session))
            {
                return ValueTask.FromResult<SessionPageResult>(new SessionReadNotFound(request.Context.ToAddress()));
            }

            var upper = session.Entries.Count == 0 ? new SessionSequence(0) : session.Entries[^1].Sequence;
            var page = session.Entries
                .Where(entry => entry.Sequence.Value > request.FromSequenceExclusive.Value)
                .Take(request.PageSize)
                .ToImmutableArray();
            var through = page.IsEmpty ? request.FromSequenceExclusive : page[^1].Sequence;
            return ValueTask.FromResult<SessionPageResult>(new SessionPage(
                page,
                through,
                hasMore: through.Value < upper.Value,
                new SessionReadSnapshot(request.Context.ToAddress(), request.BranchId, session.Version, upper)));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Branching is outside this double's scope.");

    /// <inheritdoc/>
    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Deletion is outside this double's scope.");

    /// <inheritdoc/>
    public ValueTask<SessionLaneStateResult> LoadLaneStateAsync(
        SessionLaneStateRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(request.Context.SessionId, out var stored))
            {
                return ValueTask.FromResult<SessionLaneStateResult>(new SessionLaneStateUnavailable("The session is unavailable."));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            return ValueTask.FromResult<SessionLaneStateResult>(
                stored.Lanes.TryGetValue(laneId, out var lane)
                    ? new SessionLaneStateLoaded(new SessionLaneState(laneId, lane.Revision, lane.BranchCursor, lane.AcceptedState))
                    : new SessionLaneStateNotProvisioned("The execution lane has not been provisioned."));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionExecutionLaneProvisionResult> ProvisionLaneAsync(
        SessionExecutionLaneProvisionRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(request.Context.SessionId, out var stored))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(new SessionExecutionLaneProvisionRejected("The session is unavailable."));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (stored.Lanes.ContainsKey(laneId))
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(new SessionExecutionLaneProvisionConflict("The execution lane is already provisioned."));
            }

            if (stored.Version != request.ExpectedVersion)
            {
                return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(new SessionExecutionLaneProvisionConflict("The expected session version is stale."));
            }

            var revision = new SessionLaneRevision(1);
            stored.Lanes[laneId] = new LaneRecord(request.BranchCursor, revision);
            return ValueTask.FromResult<SessionExecutionLaneProvisionResult>(
                new SessionExecutionLaneProvisioned(laneId, request.BranchCursor, revision, stored.Version, existing: false));
        }
    }

    /// <inheritdoc/>
    public ValueTask<InputAdmissionResult> AdmitInputAsync(
        SessionInputAdmissionRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(request.Context.SessionId, out var stored))
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.AddressNotFound, "The session is unavailable.")));
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!stored.Lanes.TryGetValue(laneId, out var lane))
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.AddressNotFound, "The execution lane is not provisioned.")));
            }

            if (stored.Version != request.ExpectedVersion || lane.Revision != request.ExpectedLaneRevision || lane.BranchCursor != request.BranchCursor)
            {
                return ValueTask.FromResult<InputAdmissionResult>(new RejectedInput(
                    new InputRejection(InputRejectionKind.StaleVersion, "The expected lane revision, branch cursor, or session version is stale.")));
            }

            var sequence = new SessionSequence(stored.Entries.Count + 1);
            stored.Admissions[request.AdmissionId] = request.EffectivePayload;
            lane.BranchCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, request.EntryId);
            lane.Revision = new SessionLaneRevision(lane.Revision.Value + 1);
            stored.Version = new SessionVersion(stored.Version.Value + 1);
            var receipt = new AdmissionReceipt(
                request.AdmissionId, request.OriginalPayload.Id, request.Context.AgentId, request.Context.SessionId,
                laneId, sequence, existing: false);
            return ValueTask.FromResult<InputAdmissionResult>(new AcceptedInput(receipt));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SessionRunStartResult> AcceptRunAsync(
        SessionRunStartRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (AcceptRunGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        lock (_gate)
        {
            if (AcceptRunOverride is { } scripted)
            {
                AcceptRunOverride = null;
                return scripted;
            }

            if (!_sessions.TryGetValue(request.Context.SessionId, out var stored))
            {
                return new SessionRunStartRejected("The session is unavailable.");
            }

            var laneId = request.Context.ExecutionLaneId!.Value;
            if (!stored.Lanes.TryGetValue(laneId, out var lane))
            {
                return new SessionRunStartRejected("The execution lane is not provisioned.");
            }

            if (lane.AcceptedState is { } active)
            {
                return new SessionRunStartBusy(active.Correlation.OperationId, active.Correlation.RunId);
            }

            if (lane.Revision != request.ExpectedLaneRevision || stored.Version != request.ExpectedVersion)
            {
                return new SessionRunStartConflict(
                    SessionRunStartConflictKind.LaneRevision, "The expected lane revision or session version is stale.");
            }

            var correlation = new InRunOperationCorrelation(request.Context.Correlation.OperationId, request.RunId, request.InitialTurnId);
            var appended = new List<SessionEntry>(request.SelectedAdmissionIds.Length);
            SessionEntryId? parent = stored.Entries.Count == 0 ? null : stored.Entries[^1].Id;
            for (var index = 0; index < request.SelectedAdmissionIds.Length; index++)
            {
                var payload = stored.Admissions[request.SelectedAdmissionIds[index]];
                var message = new UserMessage(
                    request.MessageIds[index], request.Context.AgentId, request.Context.SessionId, stored.Descriptor.ConversationId,
                    lane.BranchCursor.BranchId, request.RunId, request.InitialTurnId, request.AcceptedAt, MessageState.Complete,
                    payload.Parts, payload.Extensions);
                var entry = new MessageSessionEntry(
                    request.EntryIds[index], request.Context.ToAddress(), correlation, lane.BranchCursor.BranchId,
                    new SessionSequence(stored.Entries.Count + appended.Count + 1), parent, request.AcceptedAt,
                    new SchemaVersion("1"), message);
                appended.Add(entry);
                parent = entry.Id;
            }

            var installedRevision = new SessionLaneRevision(lane.Revision.Value + 1);
            var newVersion = new SessionVersion(stored.Version.Value + 1);
            var committedCursor = new SessionBranchCursor(lane.BranchCursor.BranchId, request.AcceptedEntryId);
            var state = new SessionAcceptedRunState(
                request.Context.ToAddress(), laneId, installedRevision, correlation, request.OperationStateRevision,
                request.Context.Identity, request.InRunAuthorization, request.SessionProfile, request.Configuration,
                lane.BranchCursor, committedCursor, request.PromotionCutoff, request.InitiatingAdmissionId,
                request.SelectedAdmissionIds, request.EntryIds, request.MessageIds, request.InitialTurnId, request.AcceptedAt);

            stored.Entries.AddRange(appended);
            stored.Version = newVersion;
            lane.Revision = installedRevision;
            lane.BranchCursor = committedCursor;
            lane.AcceptedState = state;
            return new SessionRunAccepted(state, newVersion, existing: false);
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionRunStateResult> LoadRunStateAsync(
        SessionRunStateRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return !_sessions.TryGetValue(request.Context.SessionId, out var stored)
                || !stored.Lanes.TryGetValue(request.Context.ExecutionLaneId!.Value, out var lane)
                || lane.AcceptedState is not { } state
                || state.Correlation != request.Context.Correlation
                ? ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateUnavailable("The requested operation state is unavailable."))
                : ValueTask.FromResult<SessionRunStateResult>(new SessionRunStateLoaded(state));
        }
    }

    /// <inheritdoc/>
    public ValueTask<SessionRunReleaseResult> ReleaseRunAsync(
        SessionRunReleaseRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(request.Context.SessionId, out var stored)
                || !stored.Lanes.TryGetValue(request.ExecutionLaneId, out var lane))
            {
                return ValueTask.FromResult<SessionRunReleaseResult>(
                    new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.LaneNotFound, "The selected lane does not exist."));
            }

            if (lane.AcceptedState is not { } active)
            {
                return ValueTask.FromResult<SessionRunReleaseResult>(
                    new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.NoAcceptedRun, "The selected lane holds no accepted run."));
            }

            if (active.Correlation.OperationId != request.OperationId
                || active.Correlation.RunId != request.RunId
                || active.OperationStateRevision != request.ExpectedStateRevision)
            {
                return ValueTask.FromResult<SessionRunReleaseResult>(
                    new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.Fenced, "The lane's installed accepted run does not match the requested operation, run, and state revision."));
            }

            if (stored.Version != request.ExpectedVersion)
            {
                return ValueTask.FromResult<SessionRunReleaseResult>(
                    new SessionRunReleaseRejected(SessionRunReleaseRejectionKind.SessionVersion, "The expected session version is stale."));
            }

            lane.AcceptedState = null;
            stored.Version = new SessionVersion(stored.Version.Value + 1);
            return ValueTask.FromResult<SessionRunReleaseResult>(new SessionRunReleased(stored.Version, existing: false));
        }
    }

    private sealed class StoredSession(SessionDescriptor descriptor)
    {
        public SessionDescriptor Descriptor { get; } = descriptor;

        public SessionVersion Version { get; set; } = descriptor.Version;

        public List<SessionEntry> Entries { get; } = [];

        public Dictionary<ExecutionLaneId, LaneRecord> Lanes { get; } = [];

        public Dictionary<AdmissionId, AgentInput> Admissions { get; } = [];
    }

    private sealed class LaneRecord(SessionBranchCursor branchCursor, SessionLaneRevision revision)
    {
        public SessionBranchCursor BranchCursor { get; set; } = branchCursor;

        public SessionLaneRevision Revision { get; set; } = revision;

        public SessionAcceptedRunState? AcceptedState { get; set; }
    }
}
