// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One request to write a file through an <see cref="IFileSystem"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record FileWriteRequest
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteRequest"/> record.</summary>
    /// <param name="path">The path to write, relative to the file system's configured root.</param>
    /// <param name="content">The text content to write.</param>
    /// <param name="mode">How to treat an existing file at <paramref name="path"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="mode"/> is not a defined <see cref="FileWriteMode"/> value.
    /// </exception>
    public FileWriteRequest(FileSystemPath path, string content, FileWriteMode mode)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfUndefined(mode);

        Path = path;
        Content = content;
        Mode = mode;
    }

    /// <summary>Gets the path to write, relative to the file system's configured root.</summary>
    public FileSystemPath Path { get; init; }

    /// <summary>Gets the text content to write.</summary>
    public string Content { get; init; }

    /// <summary>Gets how to treat an existing file at <see cref="Path"/>.</summary>
    public FileWriteMode Mode { get; init; }
}
