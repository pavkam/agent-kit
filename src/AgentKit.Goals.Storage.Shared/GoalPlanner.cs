// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Plans creations and transitions over any <see cref="IGoalLookup"/>, so every adapter applies identical idempotency, parentage, ownership, and reduction rules.</summary>
/// <remarks>Planning is side-effect free: it reads through the lookup and returns the aggregate to persist. The adapter persists it atomically with whatever gate or transaction made its reads consistent.</remarks>
internal static class GoalPlanner
{
    /// <summary>Plans one goal creation.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="request">The create request, already authorized.</param>
    /// <returns>An applied plan, a replay, or a typed rejection.</returns>
    internal static GoalPlan PlanCreate(IGoalLookup lookup, TenantId tenant, GoalCreateRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var key = request.IdempotencyKey.Value;
        if (lookup.FindByCreationKey(tenant, key) is { } stored)
        {
            return GoalRecordReducer.IsCreationEquivalent(stored, request)
                ? GoalPlan.Replayed(tenant, stored)
                : GoalPlan.Rejected(tenant, GoalStoreFailureKind.IdempotencyConflict, "The creation idempotency key was reused with a different request.");
        }

        if (lookup.Find(tenant, request.Goal.Id) is not null)
        {
            return GoalPlan.Rejected(tenant, GoalStoreFailureKind.IdempotencyConflict, "The goal identity is already in use.");
        }

        int? ordinal = null;
        if (request.Goal.ParentId is { } parentId)
        {
            var existing = lookup.CountChildren(tenant, parentId);
            if (GoalRecordReducer.ValidateParent(lookup.Find(tenant, parentId), request.Goal, existing) is { } failure)
            {
                return GoalPlan.Rejected(tenant, failure);
            }

            ordinal = existing + 1;
        }

        return GoalPlan.Applied(tenant, key, GoalRecordReducer.Create(request, lookup.NextSequence, ordinal));
    }

    /// <summary>Plans one goal transition.</summary>
    /// <param name="lookup">The stored-state reader.</param>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="request">The transition request, already authorized.</param>
    /// <returns>An applied plan, a replay, or a typed rejection.</returns>
    internal static GoalPlan PlanTransition(IGoalLookup lookup, TenantId tenant, GoalTransitionRequest request)
    {
        Debug.Assert(lookup is not null, "Adapters supply a lookup.");
        Debug.Assert(request is not null, "Adapters validate the request before planning it.");
        var current = lookup.Find(tenant, request.Transition.GoalId);
        if (current is null)
        {
            return GoalPlan.Rejected(tenant, GoalStoreFailureKind.NotFound, "The goal does not exist.");
        }

        if (current.Goal.OwnerAgentId != request.Transition.OwnerAgentId || current.Goal.SessionId != request.Transition.SessionId)
        {
            return GoalPlan.Rejected(tenant, GoalStoreFailureKind.ScopeMismatch, "The transition addresses another agent's or session's goal.");
        }

        var reduction = GoalRecordReducer.ApplyTransition(current, request, lookup.NextSettledSequence);
        return reduction.Kind switch
        {
            GoalReductionKind.Applied => GoalPlan.Applied(tenant, null, reduction.Record!),
            GoalReductionKind.Replayed => GoalPlan.Replayed(tenant, reduction.Record!),
            GoalReductionKind.Rejected => GoalPlan.Rejected(tenant, reduction.Failure!),
            _ => throw new UnreachableException(),
        };
    }
}
