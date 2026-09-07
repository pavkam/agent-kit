// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the effecting store rejected its presented grant.</summary>
public sealed record PlanStateDenied: PlanStateResult
{
    /// <summary>Initializes a denied result.</summary>
    /// <param name="safeMessage">A non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public PlanStateDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
