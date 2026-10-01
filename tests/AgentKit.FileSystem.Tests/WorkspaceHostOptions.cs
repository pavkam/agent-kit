// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

/// <summary>Mutable test-side description of one workspace host: a root plus every read, write, search, and patch ceiling.</summary>
/// <remarks>Defaults match <see cref="OperatingSystemFileSystemOptions"/>; <see cref="WorkspaceHostTestFactory"/> projects an instance onto the immutable profile snapshot the production host consumes.</remarks>
internal sealed class WorkspaceHostOptions
{
    /// <summary>Gets or sets the absolute root directory the host observes.</summary>
    public string RootDirectory { get; set; } = "";

    /// <summary>Gets or sets the profile-wide read ceiling in bytes.</summary>
    public long MaximumReadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the profile-wide write ceiling in bytes.</summary>
    public long MaximumWriteBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the directory snapshot entry ceiling.</summary>
    public int MaximumDirectorySnapshotEntries { get; set; } = FileSystemWorkspaceBounds.Default.MaximumDirectorySnapshotEntries;

    /// <summary>Gets or sets the search depth ceiling.</summary>
    public int MaximumSearchDepth { get; set; } = FileSystemWorkspaceBounds.Default.MaximumSearchDepth;

    /// <summary>Gets or sets the searched file ceiling.</summary>
    public int MaximumSearchFiles { get; set; } = FileSystemWorkspaceBounds.Default.MaximumSearchFiles;

    /// <summary>Gets or sets the searched byte ceiling.</summary>
    public long MaximumSearchBytes { get; set; } = FileSystemWorkspaceBounds.Default.MaximumSearchBytes;

    /// <summary>Gets or sets the retained match ceiling.</summary>
    public int MaximumSearchMatches { get; set; } = FileSystemWorkspaceBounds.Default.MaximumSearchMatches;

    /// <summary>Gets or sets the retained line byte ceiling.</summary>
    public int MaximumSearchLineBytes { get; set; } = FileSystemWorkspaceBounds.Default.MaximumSearchLineBytes;

    /// <summary>Gets or sets the search duration ceiling.</summary>
    public TimeSpan MaximumSearchDuration { get; set; } = FileSystemWorkspaceBounds.Default.MaximumSearchDuration;

    /// <summary>Gets or sets the patch entry ceiling.</summary>
    public int MaximumPatchEntries { get; set; } = FileSystemWorkspaceBounds.Default.MaximumPatchEntries;

    /// <summary>Gets or sets the aggregate patch byte ceiling.</summary>
    public long MaximumPatchBytes { get; set; } = FileSystemWorkspaceBounds.Default.MaximumPatchBytes;
}
