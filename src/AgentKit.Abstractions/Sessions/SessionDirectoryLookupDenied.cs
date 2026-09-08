// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that a directory lookup was denied without disclosing location existence.</summary>
/// <remarks>The safe message must be identical for absent and tenant-masked routes where a caller could otherwise infer protected state.</remarks>
public sealed record SessionDirectoryLookupDenied: SessionLocationResult
{
    /// <summary>Initializes a tenant-safe lookup denial.</summary>
    /// <param name="safeMessage">The nonblank caller-safe denial description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryLookupDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe denial description.</summary><value>Nonblank text with no session-existence or storage detail.</value>
    public string SafeMessage { get; }
}
