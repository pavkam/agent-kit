// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Declares the traversal, search, and patch ceilings an operating-system profile enforces on workspace operations.</summary>
/// <remarks>
/// The bounds apply to directory enumeration, glob, content search, snapshot reads, atomic replacement, and patch
/// application against the profile's first registered root. An operation may tighten a ceiling in its own request but can
/// never exceed the profile value. The record is immutable and safe to share across threads.
/// </remarks>
public sealed record FileSystemWorkspaceBounds
{
    /// <summary>Gets the profile defaults: 10,000 directory entries, depth 100, 10,000 searched files, 100 MiB searched, 10,000 matches, 64 KiB lines, one minute, 100 patch entries, 50 MiB patch bytes.</summary>
    /// <value>An immutable bounds value suitable for most workspaces.</value>
    public static FileSystemWorkspaceBounds Default { get; } = new(
        maximumDirectorySnapshotEntries: 10_000,
        maximumSearchDepth: 100,
        maximumSearchFiles: 10_000,
        maximumSearchBytes: 100L * 1024 * 1024,
        maximumSearchMatches: 10_000,
        maximumSearchLineBytes: 64 * 1024,
        maximumSearchDuration: TimeSpan.FromMinutes(1),
        maximumPatchEntries: 100,
        maximumPatchBytes: 50L * 1024 * 1024);

    /// <summary>Initializes workspace bounds.</summary>
    /// <param name="maximumDirectorySnapshotEntries">The most entries one directory listing may contain.</param>
    /// <param name="maximumSearchDepth">The deepest directory level a glob or search may visit.</param>
    /// <param name="maximumSearchFiles">The most files a glob or search may visit.</param>
    /// <param name="maximumSearchBytes">The most file bytes one search may read in total.</param>
    /// <param name="maximumSearchMatches">The most matches one search may retain.</param>
    /// <param name="maximumSearchLineBytes">The most bytes retained around one matching line.</param>
    /// <param name="maximumSearchDuration">The longest one search may run.</param>
    /// <param name="maximumPatchEntries">The most entries one patch plan may contain.</param>
    /// <param name="maximumPatchBytes">The most content bytes one patch plan may carry in aggregate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Any argument is zero or negative.</exception>
    public FileSystemWorkspaceBounds(
        int maximumDirectorySnapshotEntries,
        int maximumSearchDepth,
        int maximumSearchFiles,
        long maximumSearchBytes,
        int maximumSearchMatches,
        int maximumSearchLineBytes,
        TimeSpan maximumSearchDuration,
        int maximumPatchEntries,
        long maximumPatchBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDirectorySnapshotEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchFiles);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchMatches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSearchLineBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumSearchDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPatchEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPatchBytes);
        MaximumDirectorySnapshotEntries = maximumDirectorySnapshotEntries;
        MaximumSearchDepth = maximumSearchDepth;
        MaximumSearchFiles = maximumSearchFiles;
        MaximumSearchBytes = maximumSearchBytes;
        MaximumSearchMatches = maximumSearchMatches;
        MaximumSearchLineBytes = maximumSearchLineBytes;
        MaximumSearchDuration = maximumSearchDuration;
        MaximumPatchEntries = maximumPatchEntries;
        MaximumPatchBytes = maximumPatchBytes;
    }

    /// <summary>Gets the most entries one directory listing may contain.</summary>
    public int MaximumDirectorySnapshotEntries { get; init; }

    /// <summary>Gets the deepest directory level a glob or search may visit.</summary>
    public int MaximumSearchDepth { get; init; }

    /// <summary>Gets the most files a glob or search may visit.</summary>
    public int MaximumSearchFiles { get; init; }

    /// <summary>Gets the most file bytes one search may read in total.</summary>
    public long MaximumSearchBytes { get; init; }

    /// <summary>Gets the most matches one search may retain.</summary>
    public int MaximumSearchMatches { get; init; }

    /// <summary>Gets the most bytes retained around one matching line.</summary>
    public int MaximumSearchLineBytes { get; init; }

    /// <summary>Gets the longest one search may run.</summary>
    public TimeSpan MaximumSearchDuration { get; init; }

    /// <summary>Gets the most entries one patch plan may contain.</summary>
    public int MaximumPatchEntries { get; init; }

    /// <summary>Gets the most content bytes one patch plan may carry in aggregate.</summary>
    public long MaximumPatchBytes { get; init; }
}
