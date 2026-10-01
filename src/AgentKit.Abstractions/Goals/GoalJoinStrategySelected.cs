// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the join strategy a selector chose.</summary>
public sealed record GoalJoinStrategySelected: GoalJoinStrategySelectionResult
{
    /// <summary>Initializes a selected result.</summary>
    /// <param name="strategy">The borrowed strategy instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="strategy"/> is null.</exception>
    public GoalJoinStrategySelected(IGoalJoinStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        Strategy = strategy;
    }

    /// <summary>Gets the borrowed strategy instance.</summary>
    public IGoalJoinStrategy Strategy { get; }
}
