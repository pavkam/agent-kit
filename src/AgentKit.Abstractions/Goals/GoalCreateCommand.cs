// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the goal coordinator to authorize and durably create one goal.</summary>
/// <remarks>
/// The command carries captured authorization rather than a grant: the coordinator selects the authority that authorization
/// names, asks it for a single-use grant bound to this exact creation, and builds the store request. The authorization's
/// scope must be the goal's owning agent and session.
/// </remarks>
public sealed record GoalCreateCommand
{
    /// <summary>Initializes a validated create command.</summary>
    /// <param name="goal">The goal to create, as <see cref="GoalStatus.Proposed"/> or <see cref="GoalStatus.Ready"/>.</param>
    /// <param name="delegation">The delegation that creates this goal as a child, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="authorization">The captured authorization whose scope owns the goal.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The goal is not proposed or ready, the key is blank, the authorization names another agent or session, or a delegation does not describe a child of the goal's parent.</exception>
    public GoalCreateCommand(AgentGoal goal, DelegationRequest? delegation, IdempotencyKey idempotencyKey, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNotEqual(goal.Status is GoalStatus.Proposed or GoalStatus.Ready, true, nameof(goal));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, goal.OwnerAgentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, goal.SessionId, nameof(authorization));
        if (delegation is not null)
        {
            ArgumentException.ThrowIfNotEqual(goal.ParentId, delegation.ParentGoalId, nameof(delegation));
            ArgumentException.ThrowIfNotEqual(goal.OwnerAgentId, delegation.ParentAgentId, nameof(delegation));
            ArgumentException.ThrowIfNotEqual(goal.SessionId, delegation.ParentSessionId, nameof(delegation));
        }

        Goal = goal;
        Delegation = delegation;
        IdempotencyKey = idempotencyKey;
        Authorization = authorization;
    }

    /// <summary>Gets the goal to create.</summary>
    public AgentGoal Goal { get; }

    /// <summary>Gets the delegation that creates this goal as a child, or <see langword="null"/>.</summary>
    public DelegationRequest? Delegation { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the captured authorization whose scope owns the goal.</summary>
    public SecurityAuthorizationContext Authorization { get; }

    /// <summary>Gets the profile reference implied by the goal's captured profile.</summary>
    public GoalProfileReference Profile => new(Goal.ProfileKey, Goal.ProfileVersion);
}
