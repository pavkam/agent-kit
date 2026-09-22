// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Limits how many bytes a file reader may actually obtain from the backing
/// store for one operation.
/// </summary>
/// <remarks>
/// Bounds apply to bytes consumed from the stream, not merely to metadata
/// length observed before opening the file.
/// </remarks>
public readonly record struct FileReadBounds
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileReadBounds"/> struct.
    /// </summary>
    /// <param name="maxBytes">The maximum number of bytes that may be exposed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    public FileReadBounds(long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        MaxBytes = maxBytes;
    }

    /// <summary>Gets the maximum number of bytes that may be exposed.</summary>
    public long MaxBytes { get; }
}
