// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Reports that a required audit or grant-store prerequisite was unavailable.</summary>
internal sealed record DirectoryAccessUnavailable: DirectoryAccessResult
{
    /// <summary>Initializes a caller-safe unavailable result.</summary>
    /// <param name="safeMessage">The nonblank content-free explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    internal DirectoryAccessUnavailable(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe unavailable explanation.</summary>
    internal string SafeMessage { get; }
}
