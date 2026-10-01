// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no store could be selected for a profile.</summary>
public sealed record GoalStoreSelectionRejected: GoalStoreSelectionResult
{
    /// <summary>Initializes a rejected selection.</summary>
    /// <param name="safeMessage">The non-blank content-safe reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public GoalStoreSelectionRejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the content-safe reason.</summary>
    public string SafeMessage { get; }
}
