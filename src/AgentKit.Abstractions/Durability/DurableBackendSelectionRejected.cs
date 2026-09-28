// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no backend could be selected for the requested operation.</summary>
public sealed record DurableBackendSelectionRejected: DurableBackendSelectionResult
{
    /// <summary>Initializes a rejection outcome.</summary>
    /// <param name="safeMessage">The non-empty redacted reason.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public DurableBackendSelectionRejected(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the redacted rejection reason.</summary>
    /// <value>Nonblank content-free text explaining why selection failed, never a credential, endpoint, or payload.</value>
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
