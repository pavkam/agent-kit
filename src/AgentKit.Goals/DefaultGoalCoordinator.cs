// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Coordinates goal creation, attempt start, transitions, and reads over the store a captured profile selects.</summary>
/// <remarks>
/// <para>
/// Each operation selects the store the command's captured profile names, asks the authority the command's captured
/// authorization names for a single-use grant bound to the exact store operation, calls the store, and publishes one
/// immutable event per committed change. A denial, an unavailable authority, or an unavailable store returns a typed
/// refusal before any state is touched.
/// </para>
/// <para>
/// Events are published after the store committed, so a required sink that fails surfaces as an exception to the caller
/// without undoing the change; retrying the same command replays the committed result.
/// </para>
/// </remarks>
internal sealed class DefaultGoalCoordinator: IGoalCoordinator
{
    private readonly IGoalStoreSelector _stores;
    private readonly GoalGrantIssuer _grants;
    private readonly IIdentifierGenerator<GoalAttemptId> _attemptIds;
    private readonly IGoalEventDispatcher _events;
    private readonly TimeProvider _time;
    private readonly ILogger<DefaultGoalCoordinator> _logger;

    /// <summary>Initializes the coordinator.</summary>
    /// <param name="stores">The selector binding a profile to its store.</param>
    /// <param name="grants">The issuer of single-use grants from the captured authority.</param>
    /// <param name="attemptIds">The allocator of attempt identities for attempts the caller did not name.</param>
    /// <param name="events">The dispatcher delivering committed events.</param>
    /// <param name="time">The injected clock for transition instants and observation.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public DefaultGoalCoordinator(
        IGoalStoreSelector stores,
        GoalGrantIssuer grants,
        IIdentifierGenerator<GoalAttemptId> attemptIds,
        IGoalEventDispatcher events,
        TimeProvider time,
        ILogger<DefaultGoalCoordinator>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(attemptIds);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(time);
        _stores = stores;
        _grants = grants;
        _attemptIds = attemptIds;
        _events = events;
        _time = time;
        _logger = logger ?? NullLogger<DefaultGoalCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public async ValueTask<GoalCreateResult> CreateAsync(GoalCreateCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = Start(AgentKitActivityNames.GoalCreate, request.Goal.Id, request.Authorization);
        try
        {
            var (store, refusal) = await SelectAsync(request.Profile, cancellationToken).ConfigureAwait(false);
            if (store is null)
            {
                return Finish(scope, "create", new GoalCreateRejected(refusal!), refusal);
            }

            var issue = await _grants.IssueAsync(
                request.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Create,
                [GoalSecurityBinding.Resource(request.Goal.Id)],
                GoalSecurityBinding.CreateFingerprint(request.Goal, request.Delegation, request.IdempotencyKey),
                notAfter: null, hooks: null, cancellationToken).ConfigureAwait(false);
            if (issue.Grant is null)
            {
                var failure = Refusal(issue);
                return Finish(scope, "create", new GoalCreateRejected(failure), failure);
            }

            var result = await store.CreateAsync(
                new GoalCreateRequest(request.Goal, request.Delegation, request.IdempotencyKey, issue.Grant), cancellationToken).ConfigureAwait(false);
            if (result is GoalCreated created)
            {
                GoalCoordinationObservation.Safe(() => GoalLog.GoalCreated(
                    _logger, created.Record.Goal.Id, created.Record.Goal.ParentId, created.Record.Goal.OwnerAgentId, created.Record.Goal.SessionId, created.Replayed));
                if (!created.Replayed)
                {
                    await _events.PublishAsync(
                        GoalEventFactory.For(GoalEventKind.GoalCreated, created.Record, request.Authorization.Identity.TenantId, null, _time.GetUtcNow()),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return Finish(scope, "create", result, (result as GoalCreateRejected)?.Failure);
        }
        catch (Exception exception) when (Failed(scope, "create", exception))
        {
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GoalAttemptResult> StartAttemptAsync(GoalAttemptRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = Start(AgentKitActivityNames.GoalAttemptStart, request.GoalId, request.Authorization);
        try
        {
            var now = _time.GetUtcNow();
            var attemptId = request.AttemptId ?? _attemptIds.Create();
            var attempt = new GoalAttempt(
                attemptId, request.GoalId, request.AttemptAgentId, request.AttemptSessionId, request.AttemptRunId, request.AttemptNumber,
                GoalAttemptStatus.Running, request.Reservation, outcome: null, now, endedAt: null);
            var transition = new GoalTransition(
                request.GoalId, request.Authorization.Scope.AgentId, request.Authorization.Scope.SessionId!.Value, request.RequestingRunId,
                request.OperationId, GoalStatus.Ready, GoalStatus.Active, request.Actor, GoalTransitionReason.AttemptStarted,
                request.ExpectedVersion, request.IdempotencyKey, now);
            var moved = await TransitionCoreAsync(
                new GoalTransitionCommand(request.Profile, transition, new GoalAttemptStart(attempt), request.Authorization), cancellationToken).ConfigureAwait(false);
            return moved switch
            {
                GoalTransitioned transitioned => Finish(
                    scope, "attempt_start", new GoalAttemptStarted(transitioned.Record, transitioned.Record.ActiveAttempt ?? attempt), null),
                GoalTransitionRejected rejected => Finish(scope, "attempt_start", new GoalAttemptRejected(rejected.Failure), rejected.Failure),
                _ => throw new UnreachableException(),
            };
        }
        catch (Exception exception) when (Failed(scope, "attempt_start", exception))
        {
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GoalTransitionResult> TransitionAsync(GoalTransitionCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = Start(AgentKitActivityNames.GoalTransition, request.Transition.GoalId, request.Authorization);
        try
        {
            var result = await TransitionCoreAsync(request, cancellationToken).ConfigureAwait(false);
            return Finish(scope, "transition", result, (result as GoalTransitionRejected)?.Failure);
        }
        catch (Exception exception) when (Failed(scope, "transition", exception))
        {
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GoalLoadResult> LoadAsync(GoalLoadCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = Start(AgentKitActivityNames.GoalRead, request.GoalId, request.Authorization);
        try
        {
            var (store, refusal) = await SelectAsync(request.Profile, cancellationToken).ConfigureAwait(false);
            if (store is null)
            {
                return Finish(scope, "load", new GoalLoadRejected(refusal!), refusal);
            }

            var issue = await _grants.IssueAsync(
                request.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [GoalSecurityBinding.Resource(request.GoalId)], GoalSecurityBinding.LoadFingerprint(request.Profile, request.GoalId),
                notAfter: null, hooks: null, cancellationToken).ConfigureAwait(false);
            if (issue.Grant is null)
            {
                var failure = Refusal(issue);
                return Finish(scope, "load", new GoalLoadRejected(failure), failure);
            }

            var result = await store.LoadAsync(new GoalLoadRequest(request.Profile, request.GoalId, issue.Grant), cancellationToken).ConfigureAwait(false);
            return Finish(scope, "load", result, (result as GoalLoadRejected)?.Failure);
        }
        catch (Exception exception) when (Failed(scope, "load", exception))
        {
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GoalPageResult> ReadChildrenAsync(GoalChildrenCommand request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var scope = Start(AgentKitActivityNames.GoalRead, request.ParentId, request.Authorization);
        try
        {
            var (store, refusal) = await SelectAsync(request.Profile, cancellationToken).ConfigureAwait(false);
            if (store is null)
            {
                return Finish(scope, "read_children", new GoalPageRejected(refusal!), refusal);
            }

            var issue = await _grants.IssueAsync(
                request.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateRead, SecurityEffect.Observe,
                [GoalSecurityBinding.ChildrenResource(request.ParentId)],
                GoalSecurityBinding.ChildrenFingerprint(request.Profile, request.ParentId, request.AfterOrdinal, request.Limit),
                notAfter: null, hooks: null, cancellationToken).ConfigureAwait(false);
            if (issue.Grant is null)
            {
                var failure = Refusal(issue);
                return Finish(scope, "read_children", new GoalPageRejected(failure), failure);
            }

            var result = await store.ReadChildrenAsync(
                new GoalChildrenRequest(request.Profile, request.ParentId, request.AfterOrdinal, request.Limit, issue.Grant), cancellationToken).ConfigureAwait(false);
            return Finish(scope, "read_children", result, (result as GoalPageRejected)?.Failure);
        }
        catch (Exception exception) when (Failed(scope, "read_children", exception))
        {
            throw;
        }
    }

    private async ValueTask<GoalTransitionResult> TransitionCoreAsync(GoalTransitionCommand request, CancellationToken cancellationToken)
    {
        var (store, refusal) = await SelectAsync(request.Profile, cancellationToken).ConfigureAwait(false);
        if (store is null)
        {
            return new GoalTransitionRejected(refusal!);
        }

        var issue = await _grants.IssueAsync(
            request.Authorization, store.Descriptor.SecurityAudience, SecurityOperationKind.StateMutation, SecurityEffect.Mutate,
            [GoalSecurityBinding.Resource(request.Transition.GoalId)],
            GoalSecurityBinding.TransitionFingerprint(request.Profile, request.Transition, request.Attempt),
            notAfter: null, hooks: null, cancellationToken).ConfigureAwait(false);
        if (issue.Grant is null)
        {
            return new GoalTransitionRejected(Refusal(issue));
        }

        var result = await store.TransitionAsync(
            new GoalTransitionRequest(request.Profile, request.Transition, request.Attempt, issue.Grant), cancellationToken).ConfigureAwait(false);
        if (result is GoalTransitioned transitioned)
        {
            var transition = request.Transition;
            GoalCoordinationObservation.Safe(() => GoalLog.GoalTransitioned(_logger, transition.GoalId, transition.From, transition.To, transitioned.Replayed));
            if (!transitioned.Replayed)
            {
                var tenant = request.Authorization.Identity.TenantId;
                GoalCoordinationObservation.Safe(() => GoalCoordinationMetrics.RecordTransition(transition.From, transition.To));
                await _events.PublishAsync(
                    GoalEventFactory.For(GoalEventKind.GoalTransitioned, transitioned.Record, tenant, transition.From, _time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                if (request.Attempt is GoalAttemptStart)
                {
                    await _events.PublishAsync(
                        GoalEventFactory.For(GoalEventKind.AttemptStarted, transitioned.Record, tenant, transition.From, _time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                }
                else if (request.Attempt is GoalAttemptSettlement)
                {
                    await _events.PublishAsync(
                        GoalEventFactory.For(GoalEventKind.AttemptSettled, transitioned.Record, tenant, transition.From, _time.GetUtcNow()), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return result;
    }

    private async ValueTask<(IGoalStore? Store, GoalStoreFailure? Refusal)> SelectAsync(GoalProfileReference profile, CancellationToken cancellationToken)
    {
        var selection = await _stores.SelectAsync(new GoalStoreSelectionRequest(profile), cancellationToken).ConfigureAwait(false);
        return selection is GoalStoreSelected selected
            ? (selected.Store, null)
            : (null, new GoalStoreFailure(GoalStoreFailureKind.Unavailable, (selection as GoalStoreSelectionRejected)?.SafeMessage ?? "No goal store could be selected."));
    }

    private static GoalStoreFailure Refusal(GoalGrantIssuer.GrantIssue issue) => new(
        issue.IsUnavailable ? GoalStoreFailureKind.Unavailable : GoalStoreFailureKind.Denied,
        issue.SafeMessage ?? "The operation was not authorized.");

    private static AgentKitActivityScope Start(string name, GoalId goalId, SecurityAuthorizationContext authorization) =>
        AgentKitActivityScope.Start(
            name,
            ActivityKind.Internal,
            [
                new(AgentKitTagNames.GoalId, goalId.ToString()),
                new(AgentKitTagNames.AgentId, authorization.Scope.AgentId.ToString()),
                new(AgentKitTagNames.TenantId, authorization.Identity.TenantId.Value),
            ]);

    private TResult Finish<TResult>(AgentKitActivityScope scope, string operation, TResult result, GoalStoreFailure? failure)
    {
        if (failure is null)
        {
            GoalCoordinationObservation.Safe(() => scope.Activity.SetSuccessful("completed"));
        }
        else
        {
            var outcome = GoalStoreObservation.Name(failure.Kind);
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed(outcome, outcome));
            GoalCoordinationObservation.Safe(() => GoalLog.GoalOperationRefused(_logger, operation, failure.Kind));
        }

        return result;
    }

    private bool Failed(AgentKitActivityScope scope, string operation, Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
        }
        else
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            GoalCoordinationObservation.Safe(() => scope.Activity.SetFailed("faulted", errorType));
            GoalCoordinationObservation.Safe(() => GoalLog.OperationFailed(_logger, operation, errorType));
        }

        return false;
    }
}
