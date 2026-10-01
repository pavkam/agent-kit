// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a store or coordinator to durably create one goal.</summary>
/// <remarks>
/// <para>
/// Creation is idempotent by <see cref="IdempotencyKey"/> within the authorized tenant: an equivalent replay returns the
/// original record, and the same key with a different request is refused. The goal must be created as
/// <see cref="GoalStatus.Proposed"/> or <see cref="GoalStatus.Ready"/>; the store assigns its version, creation sequence,
/// and child ordinal. A delegated child carries its <see cref="Delegation"/> so the intent survives process loss.
/// </para>
/// <para>The <see cref="Grant"/> is single-use and binds this exact operation. Its authorized agent and session must be the goal's owner.</para>
/// </remarks>
public sealed record GoalCreateRequest
{
    /// <summary>Initializes a validated create request.</summary>
    /// <param name="goal">The goal to create.</param>
    /// <param name="delegation">The delegation that creates this goal as a child, or <see langword="null"/>.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="grant">The single-use grant for this operation.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The goal is not proposed or ready, carries an attempt, the key is blank, the grant lacks captured authorization, or a delegation does not describe a child of the goal's parent.</exception>
    public GoalCreateRequest(AgentGoal goal, DelegationRequest? delegation, IdempotencyKey idempotencyKey, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentException.ThrowIfNotEqual(goal.Status is GoalStatus.Proposed or GoalStatus.Ready, true, nameof(goal));
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        if (delegation is not null)
        {
            ArgumentException.ThrowIfNotEqual(goal.ParentId, delegation.ParentGoalId, nameof(delegation));
            ArgumentException.ThrowIfNotEqual(goal.OwnerAgentId, delegation.ParentAgentId, nameof(delegation));
            ArgumentException.ThrowIfNotEqual(goal.SessionId, delegation.ParentSessionId, nameof(delegation));
        }

        Goal = goal;
        Delegation = delegation;
        IdempotencyKey = idempotencyKey;
        Grant = grant;
    }

    /// <summary>Gets the goal to create.</summary>
    public AgentGoal Goal { get; }

    /// <summary>Gets the delegation that creates this goal as a child, or <see langword="null"/>.</summary>
    public DelegationRequest? Delegation { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the single-use grant for this operation.</summary>
    public SecurityGrant Grant { get; }

    /// <summary>Gets the profile reference implied by the goal's captured profile.</summary>
    public GoalProfileReference Profile => new(Goal.ProfileKey, Goal.ProfileVersion);
}
