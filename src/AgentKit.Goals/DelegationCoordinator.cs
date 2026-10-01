// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Authorizes, records intent for, and dispatches delegated child goals, then waits on or joins them from durable state.</summary>
/// <remarks>
/// <para>
/// A delegation passes one ordered gauntlet, and every step fails closed before any child exists: the captured profile
/// resolves, the parent is active and owned by the delegator, discovery and selection name exactly one target, the policy
/// pipeline allows and possibly narrows, the join strategy and dispatcher the profile names resolve, the child's budget is
/// reserved inside the parent's, and the authority issues a single-use delegation grant bound to the exact canonical
/// request. Only then is the child goal created, in the proposed state and with its captured authorization, and handed to the
/// dispatcher.
/// </para>
/// <para>
/// Identities are derived from the parent goal and the request's idempotency key, so a retried request resolves to the one
/// existing child: it is neither recreated nor redispatched, and a child that already settled is reported from durable state.
/// The caller then waits by re-reading the child through the injected clock, parking the waiting session's worker occupancy
/// for the duration so a waiting parent never holds a permit its own child needs. A cancelled wait applies the declared
/// cancellation relationship to the child.
/// </para>
/// </remarks>
internal sealed partial class DelegationCoordinator: IDelegationCoordinator
{
    private readonly IGoalCoordinator _goals;
    private readonly IDelegationTargetCatalog _targetCatalog;
    private readonly IDelegationTargetSelector _targetSelector;
    private readonly IDelegationPolicyPipeline _policyPipeline;
    private readonly IDelegationDispatcherSelector _dispatcherSelector;
    private readonly IGoalJoinStrategySelector _joinStrategySelector;
    private readonly IGoalProfileCatalog _profiles;
    private readonly GoalGrantIssuer _grants;
    private readonly IGoalBudgetManager _budgets;
    private readonly IGoalEventDispatcher _events;
    private readonly IDelegationWaitParking _parking;
    private readonly TimeProvider _time;
    private readonly AgentGoalOptions _options;
    private readonly ILogger<DelegationCoordinator> _logger;

    /// <summary>Initializes the coordinator with every collaborator it needs explicitly; it resolves nothing ambiently.</summary>
    /// <param name="goals">The goal coordinator that persists the parent root and child goals.</param>
    /// <param name="targetCatalog">The merged delegation-target catalog.</param>
    /// <param name="targetSelector">The selector that chooses one target.</param>
    /// <param name="policyPipeline">The policy pipeline the captured profile selects.</param>
    /// <param name="dispatcherSelector">The selector binding a profile to its dispatcher.</param>
    /// <param name="joinStrategySelector">The selector resolving a declared join strategy.</param>
    /// <param name="profiles">The catalog resolving captured profile references.</param>
    /// <param name="grants">The issuer of delegation grants from the captured authority.</param>
    /// <param name="budgets">The hierarchical child-budget manager.</param>
    /// <param name="events">The dispatcher delivering committed events.</param>
    /// <param name="parking">The parking that releases a waiting run's worker occupancy.</param>
    /// <param name="time">The injected clock for deadlines and waits.</param>
    /// <param name="options">The host options.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DelegationCoordinator(
        IGoalCoordinator goals,
        IDelegationTargetCatalog targetCatalog,
        IDelegationTargetSelector targetSelector,
        IDelegationPolicyPipeline policyPipeline,
        IDelegationDispatcherSelector dispatcherSelector,
        IGoalJoinStrategySelector joinStrategySelector,
        IGoalProfileCatalog profiles,
        GoalGrantIssuer grants,
        IGoalBudgetManager budgets,
        IGoalEventDispatcher events,
        IDelegationWaitParking parking,
        TimeProvider time,
        IOptions<AgentGoalOptions> options,
        ILogger<DelegationCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(targetCatalog);
        ArgumentNullException.ThrowIfNull(targetSelector);
        ArgumentNullException.ThrowIfNull(policyPipeline);
        ArgumentNullException.ThrowIfNull(dispatcherSelector);
        ArgumentNullException.ThrowIfNull(joinStrategySelector);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(parking);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        _goals = goals;
        _targetCatalog = targetCatalog;
        _targetSelector = targetSelector;
        _policyPipeline = policyPipeline;
        _dispatcherSelector = dispatcherSelector;
        _joinStrategySelector = joinStrategySelector;
        _profiles = profiles;
        _grants = grants;
        _budgets = budgets;
        _events = events;
        _parking = parking;
        _time = time;
        _options = options.Value;
        _logger = logger ?? NullLogger<DelegationCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async Task<DelegationResult> DelegateAsync(DelegationRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = GoalCoordinationObservation.TryTimestamp(_time);
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DelegationDelegate,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.DelegationId, request.Id.ToString()),
                new(AgentKitTagNames.AgentId, request.ParentAgentId.ToString()),
                new(AgentKitTagNames.SessionId, request.ParentSessionId.ToString()),
                new(AgentKitTagNames.RunId, request.ParentRunId.ToString()),
                new(AgentKitTagNames.GoalId, request.ParentGoalId.ToString()),
                new(AgentKitTagNames.OperationId, request.OperationId.ToString()),
                new(AgentKitTagNames.TenantId, request.Authorization.Identity.TenantId.Value),
            ]);
        try
        {
            var result = await DelegateCoreAsync(request, hooks, cancellationToken).ConfigureAwait(false);
            var outcome = OutcomeOf(result);
            GoalCoordinationObservation.Safe(() =>
            {
                if (result is DelegationChildResult { Status: DelegationStatus.Succeeded or DelegationStatus.Dispatched })
                {
                    scope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    scope.Activity.SetFailed(outcome, outcome);
                }
            });
            GoalCoordinationObservation.Safe(() => GoalCoordinationMetrics.RecordDelegation(outcome, GoalCoordinationObservation.TryElapsed(_time, started)));
            return result;
        }
        catch (OperationCanceledException)
        {
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            GoalCoordinationObservation.Safe(() => GoalCoordinationMetrics.RecordDelegation("cancelled", GoalCoordinationObservation.TryElapsed(_time, started)));
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            GoalCoordinationObservation.Safe(() => GoalLog.OperationFailed(_logger, "delegate", errorType));
            GoalCoordinationObservation.Safe(() => GoalCoordinationMetrics.RecordDelegation("faulted", GoalCoordinationObservation.TryElapsed(_time, started)));
            throw;
        }
    }

    private static string OutcomeOf(DelegationResult result) => result switch
    {
        DelegationRejected rejected => "rejected_" + Stable(rejected.Rejection.Kind),
        DelegationChildResult child => child.Status switch
        {
            DelegationStatus.Succeeded => "succeeded",
            DelegationStatus.Failed => "failed",
            DelegationStatus.Cancelled => "cancelled",
            DelegationStatus.Blocked => "blocked",
            DelegationStatus.Dispatched => "dispatched",
            _ => "unknown",
        },
        _ => "unknown",
    };

    private static string Stable(DelegationRejectionKind kind) => kind switch
    {
        DelegationRejectionKind.InvalidRequest => "invalid_request",
        DelegationRejectionKind.Unauthorized => "unauthorized",
        DelegationRejectionKind.UnknownTarget => "unknown_target",
        DelegationRejectionKind.AmbiguousTarget => "ambiguous_target",
        DelegationRejectionKind.PolicyDenied => "policy_denied",
        DelegationRejectionKind.LimitExceeded => "limit_exceeded",
        DelegationRejectionKind.BudgetUnavailable => "budget_unavailable",
        DelegationRejectionKind.DeadlineElapsed => "deadline_elapsed",
        DelegationRejectionKind.HandoffUnavailable => "handoff_unavailable",
        DelegationRejectionKind.AuditUnavailable => "audit_unavailable",
        _ => "unknown",
    };

    private async Task<DelegationResult> DelegateCoreAsync(DelegationRequest request, HookDispatchContext? hooks, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (request.Deadline <= now)
        {
            return await RejectAsync(request, DelegationRejectionKind.DeadlineElapsed, "The delegation deadline had already passed.", cancellationToken).ConfigureAwait(false);
        }

        var reference = new GoalProfileReference(request.ProfileKey, request.ProfileVersion);
        if (!_profiles.TryGet(reference, out var profile))
        {
            return await RejectAsync(request, DelegationRejectionKind.InvalidRequest, "The captured goal profile is not published.", cancellationToken).ConfigureAwait(false);
        }

        if (!profile.JoinStrategies.Contains(request.JoinStrategyKey))
        {
            return await RejectAsync(request, DelegationRejectionKind.InvalidRequest, "The goal profile does not allow the declared join strategy.", cancellationToken).ConfigureAwait(false);
        }

        var (parent, parentRefusal) = await EnsureParentAsync(request, profile, cancellationToken).ConfigureAwait(false);
        if (parent is null)
        {
            return await RejectAsync(request, parentRefusal!.Value.Kind, parentRefusal.Value.Message, cancellationToken).ConfigureAwait(false);
        }

        var existing = await CountChildrenAsync(request, reference, profile, cancellationToken).ConfigureAwait(false);
        var discovery = new DelegationDiscoveryRequest(request.ParentAgentId, request.ParentSessionId, reference);
        var catalog = await _targetCatalog.CaptureAsync(discovery, cancellationToken).ConfigureAwait(false);
        var selection = await _targetSelector.SelectAsync(request, catalog, cancellationToken).ConfigureAwait(false);
        if (selection is not DelegationTargetSelected selectedTarget)
        {
            var rejection = ((DelegationTargetRejected) selection).Rejection;
            return await RejectAsync(request, rejection.Kind, rejection.SafeMessage, cancellationToken).ConfigureAwait(false);
        }

        var context = new DelegationPolicyContext(profile, parent, GoalDepth.Read(parent.Goal.Extensions), existing, selectedTarget.Target, now);
        var decision = await _policyPipeline.EvaluateAsync(request, context, cancellationToken).ConfigureAwait(false);
        if (decision is not DelegationPolicyAllowed allowed)
        {
            var rejection = ((DelegationPolicyDenied) decision).Rejection;
            return await RejectAsync(request, rejection.Kind, rejection.SafeMessage, cancellationToken).ConfigureAwait(false);
        }

        var dispatcherSelection = await _dispatcherSelector.SelectAsync(new DelegationDispatcherSelectionRequest(reference), cancellationToken).ConfigureAwait(false);
        if (dispatcherSelection is not DelegationDispatcherSelected selectedDispatcher)
        {
            return await RejectAsync(
                request, DelegationRejectionKind.HandoffUnavailable, "No delegation dispatcher is available for the captured profile.", cancellationToken).ConfigureAwait(false);
        }

        var narrowed = request.With(
            DelegationIdentity.DelegationId(request.ParentGoalId, request.IdempotencyKey),
            allowed.Scope ?? request.Scope,
            new GoalBudgetReservation(allowed.Budget ?? request.Budget.Budget));
        var reserved = await _budgets.ReserveAsync(
            new GoalBudgetReserveRequest(narrowed, parent.Goal.Budget, parent.ActiveAttempt?.Reservation.ScopeId), cancellationToken).ConfigureAwait(false);
        if (reserved is not GoalBudgetReserved reservation)
        {
            var rejection = ((GoalBudgetRejected) reserved).Rejection;
            return await RejectAsync(request, rejection.Kind, rejection.SafeMessage, cancellationToken).ConfigureAwait(false);
        }

        var canonical = narrowed.With(narrowed.Id, narrowed.Scope, reservation.Reservation);
        var issue = await _grants.IssueAsync(
            request.Authorization,
            selectedDispatcher.Dispatcher.Descriptor.SecurityAudience,
            SecurityOperationKind.Delegation,
            SecurityEffect.Create,
            [DelegationSecurityBinding.Resource(canonical.Id)],
            DelegationSecurityBinding.Fingerprint(canonical),
            request.Deadline,
            hooks,
            cancellationToken).ConfigureAwait(false);
        if (issue.Grant is null)
        {
            await ReleaseAsync(reservation.Reservation, cancellationToken).ConfigureAwait(false);
            return await RejectAsync(
                request,
                issue.IsUnavailable ? DelegationRejectionKind.AuditUnavailable : DelegationRejectionKind.Unauthorized,
                issue.SafeMessage ?? "The delegation was not authorized.",
                cancellationToken).ConfigureAwait(false);
        }

        var child = new AgentGoal(
            DelegationIdentity.ChildGoalId(request.ParentGoalId, request.IdempotencyKey),
            request.ParentGoalId,
            request.ParentAgentId,
            request.ParentSessionId,
            request.ParentRunId,
            request.ProfileKey,
            request.ProfileVersion,
            request.AgentDefinitionRevision,
            GoalStatus.Proposed,
            request.ChildGoal,
            reservation.Reservation.Budget,
            activeAttemptId: null,
            GoalRecordReducer.InitialVersion,
            now,
            GoalDepth.Stamp(ExtensionData.Empty, GoalDepth.Read(parent.Goal.Extensions) + 1));
        var created = await _goals.CreateAsync(
            new GoalCreateCommand(child, canonical, DelegationIdentity.CreationKey(request.ParentGoalId, request.IdempotencyKey), request.Authorization),
            cancellationToken).ConfigureAwait(false);
        if (created is not GoalCreated createdGoal)
        {
            await ReleaseAsync(reservation.Reservation, cancellationToken).ConfigureAwait(false);
            var failure = ((GoalCreateRejected) created).Failure;
            return await RejectAsync(request, MapFailure(failure.Kind), failure.SafeMessage, cancellationToken).ConfigureAwait(false);
        }

        var record = createdGoal.Record;
        var storedCriteria = record.Delegation?.AcceptanceCriteria ?? canonical.AcceptanceCriteria;
        if (ChildResultProjection.IsSettled(record.Goal.Status))
        {
            return await SettleAsync(request.Id, record, storedCriteria, cancellationToken).ConfigureAwait(false);
        }

        if (record.Goal.Status == GoalStatus.Proposed)
        {
            var dispatched = await selectedDispatcher.Dispatcher.DispatchAsync(
                new AuthorizedDelegation(record.Delegation ?? canonical, selectedTarget.Target, record, issue.Grant), cancellationToken).ConfigureAwait(false);
            if (dispatched is DelegationRejected rejected)
            {
                await CancelChildAsync(canonical, record, cancellationToken).ConfigureAwait(false);
                return rejected;
            }

            GoalCoordinationObservation.Safe(() => GoalLog.DelegationDispatched(_logger, request.Id, record.Goal.Id, selectedTarget.Target.AgentId));
            await _events.PublishAsync(
                GoalEventFactory.For(GoalEventKind.DelegationDispatched, record, request.Authorization.Identity.TenantId, GoalStatus.Proposed, _time.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
        }

        return await WaitAsync(request, canonical, record, storedCriteria, reference, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DelegationRejected> RejectAsync(DelegationRequest request, DelegationRejectionKind kind, string message, CancellationToken cancellationToken)
    {
        GoalCoordinationObservation.Safe(() => GoalLog.DelegationRejected(_logger, request.Id, kind));
        var rejected = new DelegationRejected(request.Id, new DelegationRejection(kind, message), ExtensionData.Empty);
        await _events.PublishAsync(
            new GoalEvent(
                GoalEventKind.DelegationRejected, request.ParentGoalId, parentGoalId: null, request.Authorization.Identity.TenantId, request.ParentAgentId,
                request.ParentSessionId, request.ProfileKey, from: null, to: null, request.Id, _time.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return rejected;
    }

    private static DelegationRejectionKind MapFailure(GoalStoreFailureKind kind) => kind switch
    {
        GoalStoreFailureKind.Denied or GoalStoreFailureKind.ScopeMismatch => DelegationRejectionKind.Unauthorized,
        GoalStoreFailureKind.LimitExceeded => DelegationRejectionKind.LimitExceeded,
        GoalStoreFailureKind.NotFound => DelegationRejectionKind.InvalidRequest,
        GoalStoreFailureKind.VersionConflict or GoalStoreFailureKind.IdempotencyConflict or GoalStoreFailureKind.InvalidTransition => DelegationRejectionKind.InvalidRequest,
        GoalStoreFailureKind.Unavailable => DelegationRejectionKind.HandoffUnavailable,
        _ => DelegationRejectionKind.HandoffUnavailable,
    };

    private async ValueTask ReleaseAsync(GoalBudgetReservation reservation, CancellationToken cancellationToken)
    {
        try
        {
            _ = await _budgets.SettleAsync(new GoalBudgetSettleRequest(reservation, GoalBudgetUsage.None), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            GoalCoordinationObservation.Safe(() => GoalLog.OperationFailed(_logger, "release_budget", exception.GetType().Name));
        }
    }

    private async ValueTask<int> CountChildrenAsync(DelegationRequest request, GoalProfileReference reference, GoalProfileSnapshot profile, CancellationToken cancellationToken)
    {
        var page = await _goals.ReadChildrenAsync(
            new GoalChildrenCommand(reference, request.ParentGoalId, 0, profile.MaximumChildrenPerGoal + 1, request.Authorization), cancellationToken).ConfigureAwait(false);
        return page is GoalPage read ? read.Items.Length : 0;
    }
}
