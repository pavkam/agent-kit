// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a directory record could not complete without creating a partial route.</summary>
/// <remarks>The message is caller-safe and omits session content, grant details, and storage diagnostics.</remarks>
public sealed record SessionDirectoryWriteUnavailable: SessionDirectoryWriteResult
{
    /// <summary>Initializes an unavailable write outcome.</summary>
    /// <param name="safeMessage">The nonblank caller-safe failure description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryWriteUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe failure description.</summary><value>Nonblank text containing no protected state.</value>
    public string SafeMessage { get; }
}
