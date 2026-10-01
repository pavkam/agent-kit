// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports what one attempt or child actually consumed.</summary>
/// <remarks>Token usage is optional because providers do not all report it; an absent value means unknown and is never treated as zero.</remarks>
public sealed record GoalBudgetUsage
{
    /// <summary>Initializes validated usage.</summary>
    /// <param name="turns">The non-negative model turns consumed.</param>
    /// <param name="toolCalls">The non-negative tool calls consumed.</param>
    /// <param name="children">The non-negative child goals created.</param>
    /// <param name="totalTokens">The non-negative total tokens, or <see langword="null"/> when the provider reported none.</param>
    /// <exception cref="ArgumentOutOfRangeException">A present count is negative.</exception>
    public GoalBudgetUsage(int turns, int toolCalls, int children, long? totalTokens = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(turns);
        ArgumentOutOfRangeException.ThrowIfNegative(toolCalls);
        ArgumentOutOfRangeException.ThrowIfNegative(children);
        if (totalTokens is { } tokens)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tokens, nameof(totalTokens));
        }

        Turns = turns;
        ToolCalls = toolCalls;
        Children = children;
        TotalTokens = totalTokens;
    }

    /// <summary>Gets usage that reports nothing consumed and no token figure.</summary>
    public static GoalBudgetUsage None { get; } = new(0, 0, 0);

    /// <summary>Gets the model turns consumed.</summary>
    public int Turns { get; }

    /// <summary>Gets the tool calls consumed.</summary>
    public int ToolCalls { get; }

    /// <summary>Gets the child goals created.</summary>
    public int Children { get; }

    /// <summary>Gets the total tokens, or <see langword="null"/> when unknown.</summary>
    public long? TotalTokens { get; }
}
