// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that durable dispatch could not complete.</summary>
public sealed record DurableDispatchFailed: DurableDispatchResult
{
    /// <summary>Initializes a failed dispatch.</summary>
    /// <param name="safeMessage">The non-empty redacted reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public DurableDispatchFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the redacted failure reason.</summary>
    /// <value>Nonblank content-free text explaining why handoff failed, never a credential, endpoint, or payload.</value>
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
