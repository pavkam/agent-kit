// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a directory record was denied before any routing mutation occurred.</summary>
/// <remarks>The safe message must omit whether the candidate address or idempotency key already has a record.</remarks>
public sealed record SessionDirectoryWriteDenied: SessionDirectoryWriteResult
{
    /// <summary>Initializes a tenant-safe directory write denial.</summary>
    /// <param name="safeMessage">The nonblank caller-safe denial description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryWriteDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe denial description.</summary><value>Nonblank text with no route-existence or storage detail.</value>
    public string SafeMessage { get; }
}
