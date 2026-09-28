// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that crash recovery was requested without a validated durability profile.</summary>
public sealed record DurabilityUnavailable
{
    /// <summary>Initializes a durability-unavailable outcome.</summary>
    /// <param name="safeMessage">The non-empty redacted reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public DurabilityUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the redacted reason.</summary>
    /// <value>Nonblank content-free text explaining why no validated durability profile is available for the request.</value>
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
