// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Reports that exact fresh grant consumption did not authorize directory access.</summary>
internal sealed record DirectoryAccessDenied: DirectoryAccessResult
{
    /// <summary>Initializes a caller-safe denial.</summary>
    /// <param name="safeMessage">The nonblank content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    internal DirectoryAccessDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe denial explanation.</summary>
    internal string SafeMessage { get; }
}
