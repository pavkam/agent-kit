// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Profile-wide byte limits enforced by operating-system file capabilities.</summary>
public sealed record FileSystemBounds
{
    /// <summary>Initializes a new instance of the <see cref="FileSystemBounds"/> record.</summary>
    /// <param name="maximumReadBytes">The maximum bytes any read may expose.</param>
    /// <param name="maximumWriteBytes">The maximum payload bytes any write may consume.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive.</exception>
    public FileSystemBounds(long maximumReadBytes, long maximumWriteBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumReadBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWriteBytes);
        MaximumReadBytes = maximumReadBytes;
        MaximumWriteBytes = maximumWriteBytes;
    }

    /// <summary>Gets the maximum bytes any read may expose.</summary>
    public long MaximumReadBytes { get; init; }

    /// <summary>Gets the maximum payload bytes any write may consume.</summary>
    public long MaximumWriteBytes { get; init; }
}
