// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One request to read a file through an <see cref="IFileSystem"/>.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record FileReadRequest
{
    /// <summary>Initializes a new instance of the <see cref="FileReadRequest"/> record.</summary>
    /// <param name="path">The path to read, relative to the file system's configured root.</param>
    public FileReadRequest(FileSystemPath path) => Path = path;

    /// <summary>Gets the path to read, relative to the file system's configured root.</summary>
    public FileSystemPath Path { get; init; }
}
