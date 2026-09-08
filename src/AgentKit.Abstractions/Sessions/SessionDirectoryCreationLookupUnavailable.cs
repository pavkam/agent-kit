// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a creation-route lookup could not complete without exposing protected state.</summary>
/// <remarks>The safe message omits the retry payload, storage diagnostics, and whether another tenant has a matching route.</remarks>
public sealed record SessionDirectoryCreationLookupUnavailable: SessionCreationLocationResult
{
    /// <summary>Initializes an unavailable creation-route lookup result.</summary>
    /// <param name="safeMessage">The nonblank caller-safe failure description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryCreationLookupUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe failure description.</summary>
    /// <value>Nonblank text containing no route existence or storage detail.</value>
    public string SafeMessage { get; }
}
