// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds goal-store requests whose grants bind the exact operation, the way a coordinator obtains them.</summary>
/// <remarks>A request's fingerprint excludes its grant, so each method builds a probe with a throwaway grant, fingerprints it, issues the real grant, and rebuilds the request.</remarks>
/// <param name="grants">The grant harness that issues and consumes grants.</param>
/// <param name="audience">The store or dispatcher audience the grants name.</param>
public sealed class GoalRequestFactory(TestGoalGrants grants, ComponentId audience)
{
    /// <summary>Builds an exactly authorized create request.</summary>
    /// <param name="goal">The goal to create.</param>
    /// <param name="authorization">The authorization whose scope is the goal's owner.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="delegation">The delegation, or null.</param>
    /// <returns>The request.</returns>
    public GoalCreateRequest Create(AgentGoal goal, SecurityAuthorizationContext authorization, string idempotencyKey, DelegationRequest? delegation = null)
    {
        var probe = new GoalCreateRequest(goal, delegation, new IdempotencyKey(idempotencyKey), Probe(authorization));
        return new GoalCreateRequest(
            goal,
            delegation,
            new IdempotencyKey(idempotencyKey),
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Create, [GoalSecurityBinding.Resource(goal.Id)], GoalSecurityBinding.Fingerprint(probe)));
    }

    /// <summary>Builds an exactly authorized load request.</summary>
    /// <param name="goalId">The goal to load.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <returns>The request.</returns>
    public GoalLoadRequest Load(GoalId goalId, SecurityAuthorizationContext authorization)
    {
        var probe = new GoalLoadRequest(GoalTestData.Profile, goalId, Probe(authorization));
        return new GoalLoadRequest(
            GoalTestData.Profile,
            goalId,
            grants.Issue(audience, authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe, [GoalSecurityBinding.Resource(goalId)], GoalSecurityBinding.Fingerprint(probe)));
    }

    /// <summary>Builds an exactly authorized transition request.</summary>
    /// <param name="transition">The transition to apply.</param>
    /// <param name="attempt">The attempt mutation, or null.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <returns>The request.</returns>
    public GoalTransitionRequest Transition(GoalTransition transition, GoalAttemptChange? attempt, SecurityAuthorizationContext authorization)
    {
        var probe = new GoalTransitionRequest(GoalTestData.Profile, transition, attempt, Probe(authorization));
        return new GoalTransitionRequest(
            GoalTestData.Profile,
            transition,
            attempt,
            grants.Issue(audience, authorization, SecurityOperationKind.StateMutation, SecurityEffect.Mutate, [GoalSecurityBinding.Resource(transition.GoalId)], GoalSecurityBinding.Fingerprint(probe)));
    }

    /// <summary>Builds an exactly authorized children request.</summary>
    /// <param name="parentId">The parent goal.</param>
    /// <param name="afterOrdinal">The exclusive cursor.</param>
    /// <param name="limit">The page size.</param>
    /// <param name="authorization">The caller's authorization.</param>
    /// <returns>The request.</returns>
    public GoalChildrenRequest Children(GoalId parentId, long afterOrdinal, int limit, SecurityAuthorizationContext authorization)
    {
        var probe = new GoalChildrenRequest(GoalTestData.Profile, parentId, afterOrdinal, limit, Probe(authorization));
        return new GoalChildrenRequest(
            GoalTestData.Profile,
            parentId,
            afterOrdinal,
            limit,
            grants.Issue(audience, authorization, SecurityOperationKind.StateRead, SecurityEffect.Observe, [GoalSecurityBinding.ChildrenResource(parentId)], GoalSecurityBinding.Fingerprint(probe)));
    }

    private SecurityGrant Probe(SecurityAuthorizationContext authorization) => grants.Issue(
        audience,
        authorization,
        SecurityOperationKind.StateRead,
        SecurityEffect.Observe,
        [new ProtectedResource(ProtectedResourceKind.ApplicationState, "probe")],
        new InputFingerprint("probe"));
}
