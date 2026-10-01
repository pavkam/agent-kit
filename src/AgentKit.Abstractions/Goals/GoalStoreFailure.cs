// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one typed goal-store refusal without echoing goal content.</summary>
public sealed record GoalStoreFailure
{
    /// <summary>Initializes a validated failure.</summary>
    /// <param name="kind">The defined failure class.</param>
    /// <param name="safeMessage">The non-blank content-safe explanation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public GoalStoreFailure(GoalStoreFailureKind kind, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Kind = kind;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the failure class.</summary>
    public GoalStoreFailureKind Kind { get; }

    /// <summary>Gets the content-safe explanation.</summary>
    public string SafeMessage { get; }
}
