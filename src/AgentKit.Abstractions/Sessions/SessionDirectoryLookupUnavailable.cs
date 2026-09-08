// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a directory lookup could not complete without exposing protected state.</summary>
/// <remarks>The message is caller-safe and must omit session content, credentials, and storage implementation diagnostics.</remarks>
public sealed record SessionDirectoryLookupUnavailable: SessionLocationResult
{
    /// <summary>Initializes an unavailable lookup outcome.</summary>
    /// <param name="safeMessage">The nonblank caller-safe failure description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryLookupUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe failure description.</summary><value>Nonblank text containing no protected state.</value>
    public string SafeMessage { get; }
}
