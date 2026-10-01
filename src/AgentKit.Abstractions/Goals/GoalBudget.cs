// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Bounds the model turns, tool calls, and child goals one goal may consume.</summary>
/// <remarks>The values are ceilings. A child's budget can only be smaller than what its parent has left, and host options are hard limits that a profile or request may reserve less of but cannot widen.</remarks>
public sealed record GoalBudget
{
    /// <summary>Initializes a validated goal budget.</summary>
    /// <param name="maximumTurns">The positive model-turn ceiling.</param>
    /// <param name="maximumToolCalls">The positive tool-call ceiling.</param>
    /// <param name="maximumChildren">The non-negative child-goal ceiling; zero makes the goal a leaf.</param>
    /// <exception cref="ArgumentOutOfRangeException">A turn or tool ceiling is not positive, or the child ceiling is negative.</exception>
    public GoalBudget(int maximumTurns, int maximumToolCalls, int maximumChildren)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTurns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumToolCalls);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumChildren);
        MaximumTurns = maximumTurns;
        MaximumToolCalls = maximumToolCalls;
        MaximumChildren = maximumChildren;
    }

    /// <summary>Gets the model-turn ceiling.</summary>
    public int MaximumTurns { get; }

    /// <summary>Gets the tool-call ceiling.</summary>
    public int MaximumToolCalls { get; }

    /// <summary>Gets the child-goal ceiling.</summary>
    public int MaximumChildren { get; }
}
