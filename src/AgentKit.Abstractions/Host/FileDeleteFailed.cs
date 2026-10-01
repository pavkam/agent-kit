// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Deletion failed with a typed host or validation error.</summary>
public sealed record FileDeleteFailed: FileDeleteResult
{
    /// <summary>Initializes a failed deletion outcome.</summary>
    /// <param name="safeMessage">A stable, non-secret diagnostic message.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public FileDeleteFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a stable, non-secret diagnostic message.</summary>
    public string SafeMessage { get; init; }
}
