// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A file already exists at the requested path and
/// <see cref="FileWriteMode.CreateNew"/> was requested through legacy
/// <see cref="IFileSystem"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
[Obsolete("Use FileWriteConflict from the spec IFileWriter contract instead.")]
public sealed record LegacyFileAlreadyExists: LegacyFileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="LegacyFileAlreadyExists"/> record.</summary>
    /// <param name="path">The requested path.</param>
    public LegacyFileAlreadyExists(FileSystemPath path) => Path = path;

    /// <summary>Gets the requested path.</summary>
    public FileSystemPath Path { get; init; }
}
