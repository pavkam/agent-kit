// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem;

/// <summary>Names the one host-authorized file-system root and profile a file-system artifact store writes through.</summary>
/// <remarks>
/// The store creates no directories: the root must already exist, and every file it writes lives directly under it. The host path
/// is sensitive bootstrap configuration and is never logged, recorded in a reference, or used as portable artifact identity.
/// </remarks>
public sealed record FileSystemArtifactTarget
{
    /// <summary>Initializes a validated target.</summary>
    /// <param name="profileKey">The file-system profile whose reader and writer the store selects.</param>
    /// <param name="rootId">The configured logical root identity every target is addressed under.</param>
    /// <param name="hostRootPath">The fully qualified host directory of the root.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="profileKey"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="rootId"/> is blank, or <paramref name="hostRootPath"/> is blank or not fully qualified.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="hostRootPath"/> is null.</exception>
    public FileSystemArtifactTarget(FileSystemProfileKey profileKey, FileRootId rootId, string hostRootPath)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(profileKey, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId.Value, nameof(rootId));
        ArgumentNullException.ThrowIfNull(hostRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostRootPath);
        if (!Path.IsPathFullyQualified(hostRootPath))
        {
            throw new ArgumentException("The file-system artifact root must be a fully qualified path.", nameof(hostRootPath));
        }

        ProfileKey = profileKey;
        RootId = rootId;
        HostRootPath = Path.GetFullPath(hostRootPath);
    }

    /// <summary>Gets the file-system profile whose reader and writer the store selects.</summary>
    public FileSystemProfileKey ProfileKey { get; }

    /// <summary>Gets the configured logical root identity.</summary>
    public FileRootId RootId { get; }

    /// <summary>Gets the normalized host directory of the root.</summary>
    public string HostRootPath { get; }
}
