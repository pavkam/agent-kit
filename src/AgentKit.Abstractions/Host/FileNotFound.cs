// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>No file exists at the requested path.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record FileNotFound: FileReadResult
{
    /// <summary>Initializes a new instance of the <see cref="FileNotFound"/> record.</summary>
    /// <param name="path">The requested path.</param>
    public FileNotFound(FileSystemPath path) => Path = path;

    /// <summary>Gets the requested path.</summary>
    public FileSystemPath Path { get; init; }
}
