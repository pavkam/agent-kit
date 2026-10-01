// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the message was not admitted and nothing was queued.</summary>
public sealed record AgentMessageRejected: AgentMessageResult
{
    /// <summary>Initializes a rejected outcome.</summary>
    /// <param name="kind">The closed reason class.</param>
    /// <param name="safeReason">A non-empty reason that contains no message content, credentials, or paths.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeReason"/> is blank.</exception>
    public AgentMessageRejected(AgentMessageRejectionKind kind, string safeReason)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeReason);
        Kind = kind;
        SafeReason = safeReason;
    }

    /// <summary>Gets the closed reason class.</summary>
    public AgentMessageRejectionKind Kind { get; }

    /// <summary>Gets the content-free reason.</summary>
    public string SafeReason { get; }
}
