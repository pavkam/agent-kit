// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a store or coordinator for one goal's complete durable state.</summary>
/// <remarks>The authorized agent and session must own the goal; a goal of another tenant is reported as not found.</remarks>
public sealed record GoalLoadRequest
{
    /// <summary>Initializes a validated load request.</summary>
    /// <param name="profile">The captured profile the goal runs under.</param>
    /// <param name="goalId">The goal to load.</param>
    /// <param name="grant">The single-use read grant.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="goalId"/> is default.</exception>
    /// <exception cref="ArgumentException">The grant lacks captured authorization.</exception>
    public GoalLoadRequest(GoalProfileReference profile, GoalId goalId, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfEqual(goalId, default);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Profile = profile;
        GoalId = goalId;
        Grant = grant;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the goal to load.</summary>
    public GoalId GoalId { get; }

    /// <summary>Gets the single-use read grant.</summary>
    public SecurityGrant Grant { get; }
}
