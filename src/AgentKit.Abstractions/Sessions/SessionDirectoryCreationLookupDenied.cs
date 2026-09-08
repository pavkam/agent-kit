// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a denied creation-route lookup without revealing route existence.</summary>
/// <remarks>The safe message is suitable for both absent and tenant-masked routes so the caller cannot use it as an existence oracle.</remarks>
public sealed record SessionDirectoryCreationLookupDenied: SessionCreationLocationResult
{
    /// <summary>Initializes a denied creation-route lookup result.</summary>
    /// <param name="safeMessage">The nonblank caller-safe denial description.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is blank.</exception>
    public SessionDirectoryCreationLookupDenied(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the caller-safe denial description.</summary>
    /// <value>Nonblank text with no route existence or storage detail.</value>
    public string SafeMessage { get; }
}
