// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the goal coordinator to authorize and load one goal's complete durable state.</summary>
public sealed record GoalLoadCommand
{
    /// <summary>Initializes a validated load command.</summary>
    /// <param name="profile">The captured profile the goal runs under.</param>
    /// <param name="goalId">The goal to load.</param>
    /// <param name="authorization">The captured authorization whose scope owns the goal.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="goalId"/> is default.</exception>
    public GoalLoadCommand(GoalProfileReference profile, GoalId goalId, SecurityAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        ArgumentNullException.ThrowIfNull(authorization);
        Profile = profile;
        GoalId = goalId;
        Authorization = authorization;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the goal to load.</summary>
    public GoalId GoalId { get; }

    /// <summary>Gets the captured authorization whose scope owns the goal.</summary>
    public SecurityAuthorizationContext Authorization { get; }
}
