// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Projects goals, attempts, and transitions over the explicitly selected session contracts.</summary>
/// <remarks>
/// <para>
/// A goal's durable state is the <see cref="GoalCreatedSessionEntry"/> and <see cref="GoalTransitionSessionEntry"/> records
/// in its owning session's append-only history, so goal changes share the session's authorization, optimistic concurrency,
/// and recovery rules and create no hidden second history. Every operation reconstructs the session's goals by replaying
/// those entries through the same reducer every other adapter runs, then appends at most one new entry against the version
/// it read, replanning if another writer got there first.
/// </para>
/// <para>
/// This is a behavioral projection, not a storage medium: it is exactly as durable as the selected session store, it never
/// persists a delegation's captured authorization, and it cannot enumerate open children across sessions, so it reports
/// no intent discovery. The instance is thread-safe and holds no state between operations.
/// </para>
/// </remarks>
public sealed class SessionBackedGoalStore: IGoalStore
{
    private const string _adapter = "session";

    private readonly ISessionCoordinator _sessions;
    private readonly SessionProfileSnapshot _profile;
    private readonly GoalStoreEnforcement _enforcement;
    private readonly IIdentifierGenerator<SessionEntryId> _entryIds;
    private readonly TimeProvider _time;
    private readonly int _maximumAppendAttempts;
    private readonly ILogger<SessionBackedGoalStore> _logger;

    /// <summary>Initializes the projection over one session coordinator and profile.</summary>
    /// <param name="sessions">The session coordinator that loads, reads, and appends; it authorizes every session operation itself.</param>
    /// <param name="profile">The session profile the goal entries are written under.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes each exact goal grant.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="entryIds">The allocator of session-entry identities.</param>
    /// <param name="time">The clock used for entry timestamps and observational duration.</param>
    /// <param name="options">The projection options.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SessionBackedGoalStoreOptions.MaximumAppendAttempts"/> is not positive.</exception>
    public SessionBackedGoalStore(
        ISessionCoordinator sessions,
        SessionProfileSnapshot profile,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IIdentifierGenerator<SessionEntryId> entryIds,
        TimeProvider time,
        SessionBackedGoalStoreOptions options,
        ILogger<SessionBackedGoalStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(entryIds);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumAppendAttempts, nameof(options));
        Descriptor = new GoalStoreDescriptor("agentkit.goals.session", new ComponentId("agentkit.goals.session"), isDurable: false, supportsIntentDiscovery: false);
        _sessions = sessions;
        _profile = profile;
        _enforcement = new GoalStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _entryIds = entryIds;
        _time = time;
        _maximumAppendAttempts = options.MaximumAppendAttempts;
        _logger = logger ?? NullLogger<SessionBackedGoalStore>.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>The descriptor claims no durability of its own because durability is the selected session store's; it never claims intent discovery.</remarks>
    public GoalStoreDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public ValueTask<GoalCreateResult> CreateAsync(GoalCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalCreateResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Create, request.Goal.Id, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                    [GoalSecurityBinding.Resource(request.Goal.Id)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalCreateRejected(denial);
                }

                if (GoalStoreEnforcement.CheckOwner(request.Grant, request.Goal.OwnerAgentId, request.Goal.SessionId) is { } mismatch)
                {
                    return new GoalCreateRejected(mismatch);
                }

                var (plan, failure) = await CommitAsync(
                    request.Grant,
                    snapshot => snapshot.State.PlanCreate(snapshot.Descriptor.TenantId, request),
                    (snapshot, plan) => new GoalCreatedSessionEntry(
                        _entryIds.Create(), snapshot.Context.ToAddress(), snapshot.Context.Correlation, snapshot.Descriptor.ActiveBranchId,
                        snapshot.NextSequence, snapshot.LastEntryId, _time.GetUtcNow(), GoalSessionEntryCodecSupport.Version,
                        WithoutDelegation(plan.Record!), new IdempotencyKey(plan.CreateKey!)),
                    $"create:{request.IdempotencyKey.Value}",
                    cancellationToken).ConfigureAwait(false);
                return failure is not null
                    ? new GoalCreateRejected(failure)
                    : new GoalCreated(plan!.Record!, plan.Kind == GoalReductionKind.Replayed);
            },
            static result => (result as GoalCreateRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalLoadResult> LoadAsync(GoalLoadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalLoadResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Load, request.GoalId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [GoalSecurityBinding.Resource(request.GoalId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalLoadRejected(denial);
                }

                var (snapshot, failure) = await ReadSnapshotAsync(request.Grant, cancellationToken).ConfigureAwait(false);
                if (failure is not null)
                {
                    return new GoalLoadRejected(failure);
                }

                var record = snapshot!.State.Find(snapshot.Descriptor.TenantId, request.GoalId);
                return record is null
                    ? new GoalLoadRejected(new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The goal does not exist."))
                    : GoalStoreEnforcement.CheckOwner(request.Grant, record.Goal.OwnerAgentId, record.Goal.SessionId) is { } mismatch
                        ? new GoalLoadRejected(mismatch)
                        : new GoalLoaded(record);
            },
            static result => (result as GoalLoadRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalTransitionResult> TransitionAsync(GoalTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync<GoalTransitionResult>(
            _logger, _time, _adapter, GoalStoreOperationKind.Transition, request.Transition.GoalId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
                    [GoalSecurityBinding.Resource(request.Transition.GoalId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalTransitionRejected(denial);
                }

                if (GoalStoreEnforcement.CheckOwner(request.Grant, request.Transition.OwnerAgentId, request.Transition.SessionId) is { } mismatch)
                {
                    return new GoalTransitionRejected(mismatch);
                }

                var (plan, failure) = await CommitAsync(
                    request.Grant,
                    snapshot => snapshot.State.PlanTransition(snapshot.Descriptor.TenantId, request),
                    (snapshot, plan) => new GoalTransitionSessionEntry(
                        _entryIds.Create(), snapshot.Context.ToAddress(), snapshot.Context.Correlation, snapshot.Descriptor.ActiveBranchId,
                        snapshot.NextSequence, snapshot.LastEntryId, _time.GetUtcNow(), GoalSessionEntryCodecSupport.Version,
                        request.Transition, request.Attempt,
                        GoalRecordReducer.AssignsSettledSequence(request.Transition.To) ? plan.Record!.SettledSequence : null),
                    $"transition:{request.Transition.GoalId}:{request.Transition.IdempotencyKey.Value}",
                    cancellationToken).ConfigureAwait(false);
                return failure is not null
                    ? new GoalTransitionRejected(failure)
                    : new GoalTransitioned(plan!.Record!, plan.Kind == GoalReductionKind.Replayed);
            },
            static result => (result as GoalTransitionRejected)?.Failure);
    }

    /// <inheritdoc/>
    public ValueTask<GoalPageResult> ReadChildrenAsync(GoalChildrenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync(
            _logger, _time, _adapter, GoalStoreOperationKind.ReadChildren, request.ParentId, request.Grant.Identity.TenantId,
            async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await _enforcement.ConsumeAsync(
                    request.Grant, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                    [GoalSecurityBinding.ChildrenResource(request.ParentId)], GoalSecurityBinding.Fingerprint(request), cancellationToken).ConfigureAwait(false) is { } denial)
                {
                    return new GoalPageRejected(denial);
                }

                var (snapshot, failure) = await ReadSnapshotAsync(request.Grant, cancellationToken).ConfigureAwait(false);
                return failure is not null
                    ? new GoalPageRejected(failure)
                    : snapshot!.State.ReadChildren(snapshot.Descriptor.TenantId, request);
            },
            static result => (result as GoalPageRejected)?.Failure);
    }

    /// <inheritdoc/>
    /// <remarks>Always rejected with <see cref="GoalStoreFailureKind.Unavailable"/>: goal entries live in individual sessions and the session contracts offer no bounded cross-session discovery.</remarks>
    public ValueTask<GoalPageResult> ReadIntentsAsync(GoalIntentScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GoalStoreObservation.ObserveAsync(
            _logger, _time, _adapter, GoalStoreOperationKind.ReadIntents, null, null,
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult<GoalPageResult>(new GoalPageRejected(new GoalStoreFailure(
                    GoalStoreFailureKind.Unavailable, "The session-backed goal store cannot enumerate open children across sessions.")));
            },
            static result => (result as GoalPageRejected)?.Failure);
    }

    private static GoalRecord WithoutDelegation(GoalRecord record) =>
        new(record.Goal, [], [], null, record.Sequence, record.ChildOrdinal, null);

    private async ValueTask<(GoalPlan? Plan, GoalStoreFailure? Failure)> CommitAsync(
        SecurityGrant grant,
        Func<SessionSnapshot, GoalPlan> plan,
        Func<SessionSnapshot, GoalPlan, SessionEntry> buildEntry,
        string appendKey,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < _maximumAppendAttempts; attempt++)
        {
            var (snapshot, failure) = await ReadSnapshotAsync(grant, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return (null, failure);
            }

            var planned = plan(snapshot!);
            if (planned.Kind == GoalReductionKind.Rejected)
            {
                return (null, planned.Failure);
            }

            if (planned.Kind == GoalReductionKind.Replayed)
            {
                return (planned, null);
            }

            var appended = await _sessions.AppendAsync(
                new SessionAppendRequest(
                    snapshot!.Context, snapshot.Descriptor.ActiveBranchId, snapshot.Descriptor.Version,
                    new IdempotencyKey($"agentkit.goals.{appendKey}"), [buildEntry(snapshot, planned)]),
                _profile,
                cancellationToken).ConfigureAwait(false);
            if (appended is SessionAppended)
            {
                return (planned, null);
            }

            if (appended is not SessionAppendConflict)
            {
                return (null, new GoalStoreFailure(GoalStoreFailureKind.Unavailable, "The session refused the goal entry."));
            }
        }

        return (null, new GoalStoreFailure(GoalStoreFailureKind.Unavailable, "The session changed too often to commit the goal entry."));
    }

    private async ValueTask<(SessionSnapshot? Snapshot, GoalStoreFailure? Failure)> ReadSnapshotAsync(
        SecurityGrant grant,
        CancellationToken cancellationToken)
    {
        var authorization = grant.Authorization;
        if (authorization.Scope.SessionId is not { } sessionId)
        {
            return (null, new GoalStoreFailure(GoalStoreFailureKind.ScopeMismatch, "The authorized scope names no session."));
        }

        var context = new SessionOperationContext(
            authorization.Scope.AgentId, sessionId, executionLaneId: null, authorization.Scope.Correlation, grant.Identity, authorization);
        var loaded = await _sessions.LoadAsync(context, _profile, cancellationToken).ConfigureAwait(false);
        if (loaded is not SessionLoaded { Descriptor: var descriptor })
        {
            return (null, new GoalStoreFailure(GoalStoreFailureKind.NotFound, "The owning session is unavailable."));
        }

        var state = new GoalStoreState();
        var cursor = new SessionSequence(0);
        var last = cursor;
        SessionEntryId? lastId = null;
        var pageSize = Math.Max(1, _profile.MaximumPageSize);
        while (true)
        {
            var page = await _sessions.ReadAsync(
                new SessionReadRequest(context, descriptor.ActiveBranchId, cursor, pageSize), _profile, cancellationToken).ConfigureAwait(false);
            if (page is not SessionPage read)
            {
                return (null, new GoalStoreFailure(GoalStoreFailureKind.Unavailable, "The owning session's history could not be read."));
            }

            foreach (var entry in read.Entries)
            {
                last = entry.Sequence;
                lastId = entry.Id;
                if (!TryReplay(state, descriptor.TenantId, entry))
                {
                    return (null, new GoalStoreFailure(GoalStoreFailureKind.Unavailable, "The owning session's goal history is inconsistent."));
                }
            }

            if (!read.HasMore || read.Entries.IsEmpty)
            {
                break;
            }

            cursor = read.ThroughSequence;
        }

        return (new SessionSnapshot(context, descriptor, state, new SessionSequence(last.Value + 1), lastId), null);
    }

    private static bool TryReplay(GoalStoreState state, TenantId tenant, SessionEntry entry)
    {
        switch (entry)
        {
            case GoalCreatedSessionEntry created:
                state.Restore(tenant, created.CreateKey.Value, created.Record);
                return true;
            case GoalTransitionSessionEntry transition:
                if (state.Find(tenant, transition.Transition.GoalId) is not { } current)
                {
                    return false;
                }

                var reduction = GoalRecordReducer.ApplyTransition(current, transition.Transition, transition.Attempt, transition.SettledSequence ?? 1);
                if (reduction.Kind != GoalReductionKind.Applied)
                {
                    return false;
                }

                state.Restore(tenant, null, reduction.Record!);
                return true;
            default:
                return true;
        }
    }

    private sealed record SessionSnapshot(
        SessionOperationContext Context,
        SessionDescriptor Descriptor,
        GoalStoreState State,
        SessionSequence NextSequence,
        SessionEntryId? LastEntryId);
}
