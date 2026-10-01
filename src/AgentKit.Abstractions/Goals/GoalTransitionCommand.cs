// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the goal coordinator to authorize and apply one goal transition, optionally with its attempt mutation.</summary>
/// <remarks>The coordinator obtains a single-use grant bound to this exact transition from the authority the captured authorization names. The authorization's scope must be the transition's owning agent and session.</remarks>
public sealed record GoalTransitionCommand
{
    /// <summary>Initializes a validated transition command.</summary>
    /// <param name="profile">The captured profile the goal runs under.</param>
    /// <param name="transition">The transition to apply.</param>
    /// <param name="attempt">The atomic attempt mutation, or <see langword="null"/>.</param>
    /// <param name="authorization">The captured authorization whose scope owns the goal.</param>
    /// <exception cref="ArgumentNullException">A required reference argument is null.</exception>
    /// <exception cref="ArgumentException">The authorization names another agent or session than the transition.</exception>
    public GoalTransitionCommand(GoalProfileReference profile, GoalTransition transition, GoalAttemptChange? attempt, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentException.ThrowIfNotEqual(authorization.Scope.AgentId, transition.OwnerAgentId, nameof(authorization));
        ArgumentException.ThrowIfNotEqual(authorization.Scope.SessionId, transition.SessionId, nameof(authorization));
        Profile = profile;
        Transition = transition;
        Attempt = attempt;
        Authorization = authorization;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the transition to apply.</summary>
    public GoalTransition Transition { get; }

    /// <summary>Gets the atomic attempt mutation, or <see langword="null"/>.</summary>
    public GoalAttemptChange? Attempt { get; }

    /// <summary>Gets the captured authorization whose scope owns the goal.</summary>
    public SecurityAuthorizationContext Authorization { get; }
}
