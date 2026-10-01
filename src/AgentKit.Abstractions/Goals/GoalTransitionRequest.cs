// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a store or coordinator to apply one goal transition, optionally with its attempt mutation.</summary>
/// <remarks>
/// <para>
/// The store applies the transition only when the stored version equals <see cref="GoalTransition.ExpectedVersion"/> and
/// the stored status equals <see cref="GoalTransition.From"/>. Replaying the same <see cref="GoalTransition.IdempotencyKey"/>
/// with an identical request returns the original result; a different request under that key is refused.
/// </para>
/// <para>Moving to <see cref="GoalStatus.Active"/> from <see cref="GoalStatus.Ready"/> requires a <see cref="GoalAttemptStart"/>; leaving an active or waiting goal for a settled status requires a matching <see cref="GoalAttemptSettlement"/>; every other transition carries no attempt change.</para>
/// </remarks>
public sealed record GoalTransitionRequest
{
    /// <summary>Initializes a validated transition request.</summary>
    /// <param name="profile">The captured profile the goal runs under.</param>
    /// <param name="transition">The transition to apply.</param>
    /// <param name="attempt">The atomic attempt mutation, or <see langword="null"/>.</param>
    /// <param name="grant">The single-use mutation grant.</param>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">The grant lacks captured authorization.</exception>
    public GoalTransitionRequest(GoalProfileReference profile, GoalTransition transition, GoalAttemptChange? attempt, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Profile = profile;
        Transition = transition;
        Attempt = attempt;
        Grant = grant;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the transition to apply.</summary>
    public GoalTransition Transition { get; }

    /// <summary>Gets the atomic attempt mutation, or <see langword="null"/>.</summary>
    public GoalAttemptChange? Attempt { get; }

    /// <summary>Gets the single-use mutation grant.</summary>
    public SecurityGrant Grant { get; }
}
