// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One child entry observed during spec directory enumeration.</summary>
public sealed record FileSystemEntry
{
    /// <summary>Initializes one enumerated entry.</summary>
    /// <param name="name">The canonical relative name within the enumerated directory.</param>
    /// <param name="isDirectory">Whether the entry is a directory.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    public FileSystemEntry(NormalizedRelativePath name, bool isDirectory)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(name, default);
        Name = name;
        IsDirectory = isDirectory;
    }

    /// <summary>Gets the canonical relative name.</summary>
    public NormalizedRelativePath Name { get; init; }

    /// <summary>Gets whether the entry is a directory.</summary>
    public bool IsDirectory { get; init; }
}
