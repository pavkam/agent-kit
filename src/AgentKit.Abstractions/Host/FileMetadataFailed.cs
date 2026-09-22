// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Metadata observation failed with a typed host or validation error.</summary>
public sealed record FileMetadataFailed: FileMetadataResult
{
    /// <summary>Initializes a failed metadata outcome.</summary>
    /// <param name="safeMessage">A stable, non-secret diagnostic message.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public FileMetadataFailed(string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        SafeMessage = safeMessage;
    }

    /// <summary>Gets a stable, non-secret diagnostic message.</summary>
    public string SafeMessage { get; init; }
}
