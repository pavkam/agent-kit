// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures hard per-child turn and tool-call ceilings requested by a parent.</summary>
public sealed record TaskDelegationBudget
{
    /// <summary>Initializes bounded child execution limits.</summary>
    /// <param name="maximumTurns">The maximum child loop turns.</param>
    /// <param name="maximumToolCalls">The maximum admitted child tool calls.</param>
    /// <exception cref="ArgumentOutOfRangeException">A bound is not positive.</exception>
    public TaskDelegationBudget(int maximumTurns, int maximumToolCalls)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTurns);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumToolCalls);
        MaximumTurns = maximumTurns;
        MaximumToolCalls = maximumToolCalls;
    }

    /// <summary>Gets the maximum child loop turns.</summary>
    public int MaximumTurns { get; init; }

    /// <summary>Gets the maximum admitted child tool calls.</summary>
    public int MaximumToolCalls { get; init; }
}
