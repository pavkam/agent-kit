// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Caller-supplied relative path text before lexical normalization.</summary>
/// <remarks>Normalization is pure lexical work and does not inspect the host file system.</remarks>
public sealed record FilePathInput
{
    /// <summary>Initializes a new instance of the <see cref="FilePathInput"/> record.</summary>
    /// <param name="rootId">The logical root the path is relative to.</param>
    /// <param name="relativePath">The raw relative path text supplied by the caller.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="relativePath"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public FilePathInput(FileRootId rootId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        RootId = rootId;
        RelativePath = relativePath;
    }

    /// <summary>Gets the logical root the path is relative to.</summary>
    public FileRootId RootId { get; init; }

    /// <summary>Gets the raw relative path text supplied by the caller.</summary>
    public string RelativePath { get; init; }
}
