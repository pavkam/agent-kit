// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Maps one logical file root to an absolute host directory.</summary>
public sealed record FileRootRegistration
{
    /// <summary>Initializes a new instance of the <see cref="FileRootRegistration"/> record.</summary>
    /// <param name="rootId">The logical root identifier.</param>
    /// <param name="hostRootPath">The absolute host directory for the root.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="hostRootPath"/> is not an absolute path.
    /// </exception>
    public FileRootRegistration(FileRootId rootId, string hostRootPath)
    {
        if (string.IsNullOrWhiteSpace(hostRootPath) || !Path.IsPathRooted(hostRootPath))
        {
            throw new ArgumentException("Host root path must be an absolute path.", nameof(hostRootPath));
        }

        RootId = rootId;
        HostRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(hostRootPath));
    }

    /// <summary>Gets the logical root identifier.</summary>
    public FileRootId RootId { get; init; }

    /// <summary>Gets the absolute host directory for the root.</summary>
    public string HostRootPath { get; init; }
}
