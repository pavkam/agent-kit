// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests exact bounded bytes for planning a later version-conditional mutation.</summary>
public sealed record FileSnapshotRequest
{
    /// <summary>Initializes a snapshot request.</summary>
    /// <param name="path">The canonical workspace-relative path.</param>
    /// <param name="maximumBytes">The positive maximum complete file size.</param>
    /// <param name="grant">The exact observation authority.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumBytes"/> is not positive.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    public FileSnapshotRequest(FileSystemPath path, long maximumBytes, SecurityGrant grant)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        ArgumentNullException.ThrowIfNull(grant);
        Path = path;
        MaximumBytes = maximumBytes;
        Grant = grant;
    }

    /// <summary>Gets the path.</summary>
    public FileSystemPath Path { get; init; }
    /// <summary>Gets the complete-file byte bound.</summary>
    public long MaximumBytes { get; init; }
    /// <summary>Gets the exact observation authority.</summary>
    public SecurityGrant Grant { get; init; }
}
