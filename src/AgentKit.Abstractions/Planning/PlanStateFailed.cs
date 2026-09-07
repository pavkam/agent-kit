// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that an authorized plan-state operation failed.</summary>
public sealed record PlanStateFailed: PlanStateResult
{
    /// <summary>Initializes a failed result.</summary>
    /// <param name="safeMessage">A non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public PlanStateFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
