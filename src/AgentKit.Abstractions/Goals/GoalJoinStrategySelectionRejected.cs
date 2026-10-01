// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the declared join strategy is unknown or not allowed by the profile.</summary>
public sealed record GoalJoinStrategySelectionRejected: GoalJoinStrategySelectionResult
{
    /// <summary>Initializes a rejected selection.</summary>
    /// <param name="safeMessage">The non-blank content-safe reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public GoalJoinStrategySelectionRejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public string SafeMessage { get; }
}
