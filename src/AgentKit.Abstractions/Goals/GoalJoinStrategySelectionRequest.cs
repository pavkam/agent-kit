// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a selector for the join strategy a parent declared, validated against its captured profile.</summary>
public sealed record GoalJoinStrategySelectionRequest
{
    /// <summary>Initializes a validated selection request.</summary>
    /// <param name="profile">The captured profile.</param>
    /// <param name="strategyKey">The declared strategy key.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="strategyKey"/> is blank.</exception>
    public GoalJoinStrategySelectionRequest(GoalProfileReference profile, GoalJoinStrategyKey strategyKey)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyKey.Value, nameof(strategyKey));
        Profile = profile;
        StrategyKey = strategyKey;
    }

    /// <summary>Gets the captured profile reference.</summary>
    public GoalProfileReference Profile { get; }

    /// <summary>Gets the declared strategy key.</summary>
    public GoalJoinStrategyKey StrategyKey { get; }
}
