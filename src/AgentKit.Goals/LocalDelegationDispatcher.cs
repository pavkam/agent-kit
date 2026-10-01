// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Commits an idempotent child-admission intent through the goal contracts and returns its handoff receipt.</summary>
/// <remarks>
/// <para>
/// The dispatcher validates and consumes its own single-use delegation grant, then durably moves the child from
/// <see cref="GoalStatus.Proposed"/> to <see cref="GoalStatus.Ready"/>: that committed goal is the intent. It never runs the
/// child and does not constructor-depend on the engine, a runner, or any callback that captures one, so the construction graph
/// stays acyclic. A host-owned worker activated after engine readiness drains intents through the public engine surface and
/// claims each with an atomic transition, so replaying a delegation or a signal never creates a second child.
/// </para>
/// <para>The wake-up signal is best effort: a failing signal is logged and ignored because the durable intent already exists and a discovery-capable store lists it.</para>
/// </remarks>
internal sealed class LocalDelegationDispatcher: IDelegationDispatcher
{
    private readonly IGoalCoordinator _goals;
    private readonly GoalStoreEnforcement _enforcement;
    private readonly IDelegationIntentSignal _signal;
    private readonly TimeProvider _time;
    private readonly ILogger<LocalDelegationDispatcher> _logger;

    /// <summary>Initializes the dispatcher.</summary>
    /// <param name="goals">The goal coordinator that persists the handoff.</param>
    /// <param name="grants">The authoritative grant store that validates and consumes the delegation grant.</param>
    /// <param name="intentIds">The allocator of enforcement-intent identities.</param>
    /// <param name="signal">The best-effort worker wake-up.</param>
    /// <param name="time">The injected clock for the transition instant and observation.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public LocalDelegationDispatcher(
        IGoalCoordinator goals,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IDelegationIntentSignal signal,
        TimeProvider time,
        ILogger<LocalDelegationDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(time);
        Descriptor = new DelegationDispatcherDescriptor(new ComponentId("agentkit.goals.local-dispatcher"), isSingletonSafe: true, isDurable: false);
        _goals = goals;
        _enforcement = new GoalStoreEnforcement(grants, intentIds, Descriptor.SecurityAudience);
        _signal = signal;
        _time = time;
        _logger = logger ?? NullLogger<LocalDelegationDispatcher>.Instance;
    }

    /// <inheritdoc/>
    /// <remarks>The descriptor claims no durability of its own: the handoff is exactly as durable as the goal store the profile selects.</remarks>
    public DelegationDispatcherDescriptor Descriptor { get; }

    /// <inheritdoc/>
    public async Task<DelegationResult> DispatchAsync(AuthorizedDelegation request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var delegation = request.Request;
        using var scope = AgentKitActivityScope.Start(
            AgentKitActivityNames.DelegationDispatch,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.DelegationId, delegation.Id.ToString()),
                new(AgentKitTagNames.GoalId, request.Child.Goal.Id.ToString()),
                new(AgentKitTagNames.AgentId, delegation.ParentAgentId.ToString()),
                new(AgentKitTagNames.TenantId, delegation.Authorization.Identity.TenantId.Value),
            ]);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant,
                SecurityOperationKind.Delegation,
                SecurityEffect.Create,
                [DelegationSecurityBinding.Resource(delegation.Id)],
                DelegationSecurityBinding.Fingerprint(delegation),
                cancellationToken).ConfigureAwait(false);
            if (denial is not null)
            {
                GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("grant_denied", "grant_denied"));
                return Rejected(delegation, DelegationRejectionKind.Unauthorized, denial.SafeMessage);
            }

            var child = request.Child;
            var ready = child;
            if (child.Goal.Status == GoalStatus.Proposed)
            {
                var now = _time.GetUtcNow();
                var transition = new GoalTransition(
                    child.Goal.Id, child.Goal.OwnerAgentId, child.Goal.SessionId, delegation.ParentRunId, delegation.OperationId,
                    GoalStatus.Proposed, GoalStatus.Ready, TransitionActor.Coordinator, GoalTransitionReason.DelegationDispatched,
                    child.Goal.Version, new IdempotencyKey($"agentkit.goals.dispatch:{child.Goal.Id}"), now);
                var moved = await _goals.TransitionAsync(
                    new GoalTransitionCommand(new GoalProfileReference(delegation.ProfileKey, delegation.ProfileVersion), transition, null, delegation.Authorization),
                    cancellationToken).ConfigureAwait(false);
                if (moved is not GoalTransitioned transitioned)
                {
                    var failure = ((GoalTransitionRejected) moved).Failure;
                    GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("handoff_unavailable", GoalStoreObservation.Name(failure.Kind)));
                    return Rejected(delegation, DelegationRejectionKind.HandoffUnavailable, failure.SafeMessage);
                }

                ready = transitioned.Record;
            }

            try
            {
                await _signal.SignalAsync(new DelegationIntent(delegation, ready), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                GoalCoordinationObservation.Safe(() => GoalLog.OperationFailed(_logger, "signal_intent", exception.GetType().Name));
            }

            GoalCoordinationObservation.Safe(() => scope.Activity.SetSuccessful("dispatched"));
            return new DelegationChildResult(
                delegation.Id, child.Goal.Id, request.Target.AgentId, null, null, null, DelegationStatus.Dispatched, null, [],
                GoalBudgetUsage.None, SideEffectCertainty.DefinitelyNotPerformed, ExtensionData.Empty);
        }
        catch (OperationCanceledException)
        {
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            throw;
        }
    }

    private static DelegationRejected Rejected(DelegationRequest delegation, DelegationRejectionKind kind, string message) =>
        new(delegation.Id, new DelegationRejection(kind, message), ExtensionData.Empty);
}
