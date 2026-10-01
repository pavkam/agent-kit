// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a join that can never be satisfied from the settled children.</summary>
public sealed record GoalJoinUnsatisfiable: GoalJoinDecision
{
    /// <summary>Initializes an unsatisfiable decision.</summary>
    /// <param name="safeMessage">The non-blank content-safe reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public GoalJoinUnsatisfiable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public string SafeMessage { get; }
}
