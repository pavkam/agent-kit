// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Storage;

/// <summary>Is the persisted form of a <see cref="GoalBudgetUsage"/>.</summary>
/// <param name="Turns">The model turns consumed.</param>
/// <param name="ToolCalls">The tool calls consumed.</param>
/// <param name="Children">The child goals created.</param>
/// <param name="TotalTokens">The total tokens, or <see langword="null"/> when unknown.</param>
internal sealed record GoalUsageDocument(int Turns, int ToolCalls, int Children, long? TotalTokens)
{
    /// <summary>Converts usage to its persisted form.</summary>
    /// <param name="value">The non-null usage.</param>
    /// <returns>The document.</returns>
    internal static GoalUsageDocument FromDomain(GoalBudgetUsage value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value.Turns, value.ToolCalls, value.Children, value.TotalTokens);
    }

    /// <summary>Restores the usage, re-running its validation.</summary>
    /// <returns>The usage.</returns>
    internal GoalBudgetUsage ToDomain() => new(Turns, ToolCalls, Children, TotalTokens);
}
