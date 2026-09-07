// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports rejection before any child identity or state was created.</summary>
public sealed record TaskDelegationRejected: TaskDelegationResult
{
    /// <summary>Initializes a safe rejection.</summary>
    /// <param name="id">The delegation identity.</param>
    /// <param name="safeMessage">The non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public TaskDelegationRejected(DelegationId id, string safeMessage) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
