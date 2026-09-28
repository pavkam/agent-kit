// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that unknown non-idempotent work requires operator or tool reconciliation.</summary>
public sealed record OperatorActionRequired
{
    /// <summary>Initializes an operator-action-required outcome.</summary>
    /// <param name="safeMessage">The non-empty redacted reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public OperatorActionRequired(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the redacted reason.</summary>
    /// <value>Nonblank content-free text explaining why no automatic recovery action is safe without a human decision.</value>
    /// <exception cref="ArgumentException">An initializer attempts to set null, empty, or whitespace-only text.</exception>
    public string SafeMessage
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(SafeMessage));
            field = value;
        }
    }
}
