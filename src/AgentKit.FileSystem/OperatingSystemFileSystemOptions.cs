// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Mutable configuration for one keyed operating-system file-system profile.</summary>
public sealed class OperatingSystemFileSystemOptions
{
    /// <summary>Gets the profile version copied into the immutable snapshot at registration.</summary>
    public FileSystemProfileVersion ProfileVersion { get; set; } = new(1);

    /// <summary>Gets the logical roots exposed by this profile.</summary>
    public IList<FileRootRegistration> Roots { get; } = [];

    /// <summary>Gets or sets the lexical path policy.</summary>
    public FilePathPolicy PathPolicy { get; set; } = new(FilePathComparisonKind.Ordinal);

    /// <summary>Gets or sets profile-wide byte bounds.</summary>
    public FileSystemBounds Bounds { get; set; } = new(10 * 1024 * 1024, 10 * 1024 * 1024);

    /// <summary>Gets or sets write policy defaults.</summary>
    public FileWritePolicy WritePolicy { get; set; } = new();

    /// <summary>Gets or sets watch policy defaults.</summary>
    public FileWatchPolicy WatchPolicy { get; set; } = new();
}
