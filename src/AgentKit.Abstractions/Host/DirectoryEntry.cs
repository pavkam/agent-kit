// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Identifies one canonical child name observed in a directory snapshot without following it.</summary>
public sealed record DirectoryEntry
{
    /// <summary>Initializes a directory entry.</summary>
    /// <param name="path">The canonical workspace-relative child path.</param>
    public DirectoryEntry(FileSystemPath path) => Path = path;

    /// <summary>Gets the canonical workspace-relative child path.</summary>
    public FileSystemPath Path { get; init; }
}
