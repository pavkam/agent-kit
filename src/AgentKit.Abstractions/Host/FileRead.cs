// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The file was read successfully.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record FileRead: FileReadResult
{
    /// <summary>Initializes a new instance of the <see cref="FileRead"/> record.</summary>
    /// <param name="content">The file's full text content.</param>
    /// <param name="bytes">The file's size in bytes at the time it was read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is negative.</exception>
    public FileRead(string content, long bytes)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);

        Content = content;
        Bytes = bytes;
    }

    /// <summary>Gets the file's full text content.</summary>
    public string Content { get; init; }

    /// <summary>Gets the file's size in bytes at the time it was read.</summary>
    public long Bytes { get; init; }
}
