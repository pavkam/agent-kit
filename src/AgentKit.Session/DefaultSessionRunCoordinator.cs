// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

/// <summary>Coordinates exact process-local ownership independently for each accepted session execution lane.</summary>
/// <remarks>Local ownership becomes acquired only after the protected session coordinator revalidates complete canonical accepted state. This implementation has no distributed-fencing capability.</remarks>
internal sealed class DefaultSessionRunCoordinator: ISessionRunCoordinator
{
    private readonly ConcurrentDictionary<(TenantId TenantId, SessionAddress Address, ExecutionLaneId LaneId),
        SessionRunSlot> _slots = new();
    private readonly IIdentifierGenerator<SessionLeaseId> _leaseIds;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _busyWaitTimeout;
    private readonly ILogger<DefaultSessionRunCoordinator> _logger;

    /// <summary>Initializes the process-local coordinator over protected canonical state.</summary>
    /// <param name="leaseIds">Generates unique lease identities.</param>
    /// <param name="timeProvider">Controls bounded local waiting deterministically.</param>
    /// <param name="options">The validated process-wide coordination ceilings.</param>
    /// <param name="logger">The optional content-free diagnostics logger.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured busy-wait timeout is negative or exceeds the platform timer ceiling.</exception>
    public DefaultSessionRunCoordinator(
        IIdentifierGenerator<SessionLeaseId> leaseIds,
        TimeProvider timeProvider,
        IOptions<AgentSessionOptions> options,
        ILogger<DefaultSessionRunCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(leaseIds);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        var configuredOptions = options.Value;
        ArgumentNullException.ThrowIfNull(configuredOptions, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(configuredOptions.BusyWaitTimeout, TimeSpan.Zero, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(configuredOptions.BusyWaitTimeout,
            AgentSessionOptions.MaximumBusyWaitTimeout, nameof(options));
        _leaseIds = leaseIds;
        _timeProvider = timeProvider;
        _busyWaitTimeout = configuredOptions.BusyWaitTimeout;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultSessionRunCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<SessionRunLeaseResult> AcquireAsync(
        SessionRunLeaseRequest request,
        SessionExecutionCapability session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        var profile = session.Profile;
        using var activity = AgentKitActivityScope.Start(AgentKitActivityNames.SessionLeaseAcquire,
            ActivityKind.Internal, SafeTags(request));
        ObserveStarted(request);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(session.RunCoordinator, this))
            {
                return Complete(activity.Activity, request,
                    new SessionRunLeaseConflict(SessionRunLeaseConflictKind.SessionProfile,
                        "The compiled session capability selected a different run coordinator."));
            }
            if (profile.RequiresDistributedFencing)
            {
                return Complete(activity.Activity, request,
                    new SessionRunLeaseUnavailable("The selected profile requires distributed fencing unavailable from the local coordinator."));
            }

            var key = (request.Context.Identity.TenantId,
                Address: new SessionAddress(request.AgentId, request.SessionId), LaneId: request.ExecutionLaneId);
            var slot = _slots.GetOrAdd(key, static _ => new SessionRunSlot());
            var entered = await EnterAsync(slot, profile.BusyBehavior, cancellationToken).ConfigureAwait(false);
            if (!entered)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SessionRunSlotOwner? activeOwner;
                lock (slot.SyncRoot)
                {
                    activeOwner = slot.Owner;
                }
                if (activeOwner is null)
                {
                    return Complete(activity.Activity, request,
                        new SessionRunLeaseUnavailable("The local lane is still validating a provisional owner."));
                }

                var loaded = await session.Coordinator.LoadRunStateAsync(
                    new SessionRunStateRequest(request.Context), session, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var validation = ValidateLoadedState(request, profile, loaded);
                if (validation is not null)
                {
                    return Complete(activity.Activity, request, validation);
                }

                lock (slot.SyncRoot)
                {
                    activeOwner = slot.Owner;
                }
                return activeOwner is null
                    ? Complete(activity.Activity, request,
                        new SessionRunLeaseUnavailable("The local lane wait ended without an observable owner."))
                    : activeOwner.OperationId == request.OperationId
                        && activeOwner.RunId == request.RunId
                        && activeOwner.StateRevision == request.ExpectedStateRevision
                        ? Complete(activity.Activity, request,
                            new SessionRunBusy(request.OperationId, request.RunId))
                        : Complete(activity.Activity, request,
                            new SessionRunLeaseConflict(SessionRunLeaseConflictKind.AcceptedState,
                                "The validated accepted operation does not own the occupied local lane."));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                ReleaseUnownedGate(slot);
                cancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                var loaded = await session.Coordinator.LoadRunStateAsync(
                    new SessionRunStateRequest(request.Context), session, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var validation = ValidateLoadedState(request, profile, loaded);
                if (validation is not null)
                {
                    ReleaseUnownedGate(slot);
                    return Complete(activity.Activity, request, validation);
                }

                cancellationToken.ThrowIfCancellationRequested();
                var leaseId = _leaseIds.Create();
                var owner = new SessionRunSlotOwner(leaseId, request.OperationId, request.RunId,
                    request.ExpectedStateRevision);
                var lease = new SessionRunLease(this, session, request, leaseId);
                lock (slot.SyncRoot)
                {
                    Debug.Assert(slot.Owner is null, "Canonical validation completes before the exact owner is published.");
                    slot.Owner = owner;
                }
                return Complete(activity.Activity, request,
                    new SessionRunLeaseAcquired(lease));
            }
            catch
            {
                ReleaseUnownedGate(slot);
                throw;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            ObserveTerminal(activity.Activity, request, "cancelled", false);
            ObserveCancelled(request);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception exception)
        {
            ObserveTerminal(activity.Activity, request, "faulted", false);
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            ObserveFaulted(request, errorType);
            throw;
        }
    }

    /// <summary>Releases a lane only when the disposing lease remains its exact owner.</summary>
    /// <param name="tenantId">The tenant partition that owns the session address.</param>
    /// <param name="address">The owning session address.</param>
    /// <param name="laneId">The exact execution lane.</param>
    /// <param name="leaseId">The lease identity attempting release.</param>
    /// <exception cref="ArgumentNullException"><paramref name="address"/> is null, or <paramref name="tenantId"/> is default and has no value.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> has an empty or whitespace value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="laneId"/> or <paramref name="leaseId"/> is default.</exception>
    internal void Release(TenantId tenantId, SessionAddress address, ExecutionLaneId laneId, SessionLeaseId leaseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId.Value, nameof(tenantId));
        ArgumentNullException.ThrowIfNull(address);
        ArgumentOutOfRangeException.ThrowIfEqual(laneId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(leaseId, default);
        if (!_slots.TryGetValue((tenantId, address, laneId), out var slot))
        {
            return;
        }

        lock (slot.SyncRoot)
        {
            if (slot.Owner?.LeaseId != leaseId)
            {
                return;
            }
            slot.Owner = null;
            _ = slot.Gate.Release();
        }
    }

    /// <summary>
    /// The maximum number of load+release attempts <see cref="ReleaseDurableStateAsync"/> makes when the store
    /// rejects a release only because the whole-session version read in an earlier attempt has since advanced.
    /// </summary>
    /// <remarks>
    /// The whole-session <see cref="SessionVersion"/> advances on every mutation of any branch or lane, not just
    /// the lane being released, so ordinary concurrent activity on a sibling lane between the version read and
    /// the release call is expected under normal multi-lane load, not a rare race. The release's real fence is
    /// <see cref="SessionRunReleaseRequest.ExpectedStateRevision"/> together with the operation/run correlation
    /// (see <see cref="SessionRunReleaseRejectionKind.Fenced"/>): a stale caller can never release a different,
    /// newer occupant of the same lane regardless of how many times the whole-session version is re-read and
    /// retried here.
    /// </remarks>
    private const int _releaseVersionRetryLimit = 5;

    /// <summary>Attempts a durable release of the lane's installed accepted run state through the protected session coordinator.</summary>
    /// <param name="session">The exact compiled capability the disposing lease was acquired through.</param>
    /// <param name="context">The lane-bound in-run context of the accepted operation being released.</param>
    /// <param name="expectedStateRevision">The accepted state's positive total-state revision.</param>
    /// <param name="leaseId">The disposing lease's identity, used only for diagnostics.</param>
    /// <param name="cancellationToken">Bounds the durable release attempt; failures and cancellation are logged, never thrown.</param>
    /// <remarks>
    /// This is a best-effort background cleanup, not a request the caller is waiting on: every failure mode —
    /// a store outage, cancellation, or an unexpected exception — is logged and swallowed. A failed attempt
    /// leaves the lane busy until a later successful release or store-level recovery; it never surfaces as an
    /// exception from lease disposal. A rejection whose kind is
    /// <see cref="SessionRunReleaseRejectionKind.SessionVersion"/> is retried up to
    /// <see cref="_releaseVersionRetryLimit"/> times with a freshly reloaded version, since it reflects only a
    /// stale read of the whole-session token — unrelated activity on a sibling lane — and not a conflict over
    /// the lane actually being released; the idempotency key is stable across retries and the store never
    /// records a receipt for a rejected attempt, so retrying is safe. Every other rejection kind is terminal.
    /// </remarks>
    internal async ValueTask ReleaseDurableStateAsync(SessionExecutionCapability session,
        SessionOperationContext context, OperationStateRevision expectedStateRevision, SessionLeaseId leaseId,
        CancellationToken cancellationToken)
    {
        Debug.Assert(session is not null, "A compiled session capability is required.");
        Debug.Assert(context is not null, "A lane-bound in-run context is required.");
        var correlation = (InRunOperationCorrelation) context.Correlation;
        try
        {
            var idempotencyKey = new IdempotencyKey(
                $"agentkit.session.lane-release:{context.ExecutionLaneId}:{correlation.OperationId}:{correlation.RunId}");

            for (var attempt = 1; attempt <= _releaseVersionRetryLimit; attempt++)
            {
                var loaded = await session.Coordinator.LoadAsync(context, session.Profile, cancellationToken)
                    .ConfigureAwait(false);
                if (loaded is not SessionLoaded sessionLoaded)
                {
                    ObserveReleaseOutcome(context, leaseId, "load_failed");
                    return;
                }

                var release = new SessionRunReleaseRequest(
                    context, expectedStateRevision, sessionLoaded.Descriptor.Version, idempotencyKey);
                var result = await session.Coordinator.ReleaseRunAsync(release, session, cancellationToken)
                    .ConfigureAwait(false);

                if (result is SessionRunReleaseRejected { Kind: SessionRunReleaseRejectionKind.SessionVersion }
                    && attempt < _releaseVersionRetryLimit)
                {
                    continue;
                }

                var outcome = result switch
                {
                    SessionRunReleased => "released",
                    SessionRunReleaseRejected rejected => $"rejected:{rejected.Kind}",
                    _ => "unsupported",
                };
                ObserveReleaseOutcome(context, leaseId, outcome);
                return;
            }
        }
        catch (OperationCanceledException)
        {
            ObserveReleaseCancelled(context, leaseId);
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            ObserveReleaseFaulted(context, leaseId, errorType);
        }
    }

    private void ObserveReleaseOutcome(SessionOperationContext context, SessionLeaseId leaseId, string outcome)
    {
        Debug.Assert(context is not null, "A lane-bound in-run context is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        var correlation = (InRunOperationCorrelation) context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationCompleted(_logger,
            AgentKitActivityNames.SessionRunRelease, context.Identity.TenantId, context.AgentId, context.SessionId,
            context.ExecutionLaneId, correlation.OperationId, correlation.RunId, correlation.TurnId,
            $"{outcome} (lease {leaseId})"));
    }

    private void ObserveReleaseCancelled(SessionOperationContext context, SessionLeaseId leaseId)
    {
        Debug.Assert(context is not null, "A lane-bound in-run context is required.");
        Debug.Assert(leaseId != default, "A non-default disposing lease identity is required.");
        var correlation = (InRunOperationCorrelation) context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationCancelled(_logger,
            AgentKitActivityNames.SessionRunRelease, context.Identity.TenantId, context.AgentId, context.SessionId,
            context.ExecutionLaneId, correlation.OperationId, correlation.RunId, correlation.TurnId));
    }

    private void ObserveReleaseFaulted(SessionOperationContext context, SessionLeaseId leaseId, string errorType)
    {
        Debug.Assert(context is not null, "A lane-bound in-run context is required.");
        Debug.Assert(leaseId != default, "A non-default disposing lease identity is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(errorType), "A bounded error type is required.");
        var correlation = (InRunOperationCorrelation) context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationFaulted(_logger,
            AgentKitActivityNames.SessionRunRelease, context.Identity.TenantId, context.AgentId, context.SessionId,
            context.ExecutionLaneId, correlation.OperationId, correlation.RunId, correlation.TurnId, errorType));
    }

    private async ValueTask<bool> EnterAsync(SessionRunSlot slot, SessionBusyBehavior behavior,
        CancellationToken cancellationToken)
    {
        Debug.Assert(slot is not null, "A materialized lane slot is required.");
        if (behavior == SessionBusyBehavior.Reject || _busyWaitTimeout == TimeSpan.Zero)
        {
            return slot.Gate.Wait(0, cancellationToken);
        }

        using var deadline = new CancellationTokenSource(_busyWaitTimeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            await slot.Gate.WaitAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            return slot.Gate.Wait(0, cancellationToken);
        }
    }

    private static void ReleaseUnownedGate(SessionRunSlot slot)
    {
        Debug.Assert(slot is not null, "A materialized lane slot is required.");
        lock (slot.SyncRoot)
        {
            Debug.Assert(slot.Owner is null, "Only a gate without an installed owner can use provisional release.");
            _ = slot.Gate.Release();
        }
    }

    private static SessionRunLeaseResult? ValidateLoadedState(SessionRunLeaseRequest request,
        SessionProfileSnapshot profile, SessionRunStateResult loaded)
    {
        Debug.Assert(request is not null, "The public boundary validated the request.");
        Debug.Assert(profile is not null, "The public boundary validated the profile.");
        Debug.Assert(loaded is not null, "A coordinator result cannot be null by contract.");
        if (loaded is SessionRunStateUnavailable unavailable)
        {
            return new SessionRunLeaseUnavailable(unavailable.SafeReason);
        }
        if (loaded is not SessionRunStateLoaded successful)
        {
            return new SessionRunLeaseUnavailable("The session coordinator returned an unsupported run-state result.");
        }

        var state = successful.State;
        if (state.OperationStateRevision != request.ExpectedStateRevision)
        {
            return new SessionRunLeaseConflict(SessionRunLeaseConflictKind.OperationStateRevision,
                "The accepted operation-state revision changed before local ownership was acquired.");
        }
        var ownerMismatch = state.Address != request.Context.ToAddress()
            || state.ExecutionLaneId != request.ExecutionLaneId
            || state.Correlation != request.Context.Correlation
            || state.Identity != request.Context.Identity
            || state.Authorization != request.Context.Authorization;
        return state.SessionProfile != profile.Reference
            ? new SessionRunLeaseConflict(SessionRunLeaseConflictKind.SessionProfile,
                "The retained accepted session profile differs from the selected profile.")
            : ownerMismatch
                ? new SessionRunLeaseConflict(SessionRunLeaseConflictKind.AcceptedState,
                "The retained accepted owner differs from the requested operation.")
                : null;
    }

    private SessionRunLeaseResult Complete(Activity? activity, SessionRunLeaseRequest request,
        SessionRunLeaseResult result)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        Debug.Assert(result is not null, "A terminal lease result is required.");
        var outcome = result switch
        {
            SessionRunLeaseAcquired => "acquired",
            SessionRunBusy => "busy",
            SessionRunLeaseConflict => "conflict",
            SessionRunLeaseUnavailable => "unavailable",
            _ => "unsupported",
        };
        ObserveTerminal(activity, request, outcome, result is SessionRunLeaseAcquired);
        ObserveCompleted(request, outcome);
        return result;
    }

    private void ObserveStarted(SessionRunLeaseRequest request)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        var correlation = (InRunOperationCorrelation) request.Context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationStarted(_logger,
            AgentKitActivityNames.SessionLeaseAcquire, request.Context.Identity.TenantId,
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.OperationId,
            request.RunId, correlation.TurnId));
    }

    private void ObserveCompleted(SessionRunLeaseRequest request, string outcome)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        var correlation = (InRunOperationCorrelation) request.Context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationCompleted(_logger,
            AgentKitActivityNames.SessionLeaseAcquire, request.Context.Identity.TenantId,
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.OperationId,
            request.RunId, correlation.TurnId, outcome));
    }

    private void ObserveCancelled(SessionRunLeaseRequest request)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        var correlation = (InRunOperationCorrelation) request.Context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationCancelled(_logger,
            AgentKitActivityNames.SessionLeaseAcquire, request.Context.Identity.TenantId,
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.OperationId,
            request.RunId, correlation.TurnId));
    }

    private void ObserveFaulted(SessionRunLeaseRequest request, string errorType)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(errorType), "A bounded error type is required.");
        var correlation = (InRunOperationCorrelation) request.Context.Correlation;
        TryObserve(() => SessionLog.CorrelatedOperationFaulted(_logger,
            AgentKitActivityNames.SessionLeaseAcquire, request.Context.Identity.TenantId,
            request.AgentId, request.SessionId, request.ExecutionLaneId, request.OperationId,
            request.RunId, correlation.TurnId, errorType));
    }

    private static void ObserveTerminal(Activity? activity, SessionRunLeaseRequest request, string outcome,
        bool successful)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        TryObserve(() =>
        {
            if (successful)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, outcome);
            }
        });
        TryObserve(() => SessionMetrics.Operations.Add(1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation,
                AgentKitActivityNames.SessionLeaseAcquire),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)));
    }

    private static IEnumerable<KeyValuePair<string, object?>> SafeTags(SessionRunLeaseRequest request)
    {
        Debug.Assert(request is not null, "A validated lease request is required.");
        return
        [
            new(AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SessionLeaseAcquire),
            new(AgentKitTagNames.TenantId, request.Context.Identity.TenantId.ToString()),
            new(AgentKitTagNames.AgentId, request.AgentId.ToString()),
            new(AgentKitTagNames.SessionId, request.SessionId.ToString()),
            new(AgentKitTagNames.ExecutionLaneId, request.ExecutionLaneId.ToString()),
            new(AgentKitTagNames.OperationId, request.OperationId.ToString()),
            new(AgentKitTagNames.RunId, request.RunId.ToString()),
            new(AgentKitTagNames.TurnId,
                ((InRunOperationCorrelation)request.Context.Correlation).TurnId?.ToString()),
            new(AgentKitTagNames.SessionOperation, AgentKitActivityNames.SessionLeaseAcquire),
        ];
    }

    private static void TryObserve(Action observation)
    {
        Debug.Assert(observation is not null, "An observation callback is required.");
        try
        {
            observation();
        }
        catch
        {
            // Diagnostics are observational and cannot change ownership semantics.
        }
    }
}
