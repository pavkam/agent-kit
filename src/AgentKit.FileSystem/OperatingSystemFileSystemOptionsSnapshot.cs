// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Immutable operating-system file profile captured at registration time.</summary>
internal sealed record OperatingSystemFileSystemOptionsSnapshot(
    FileSystemProfileKey ProfileKey,
    FileSystemProfileVersion ProfileVersion,
    ImmutableArray<FileRootRegistration> Roots,
    FilePathPolicy PathPolicy,
    FileSystemBounds Bounds,
    FileWritePolicy WritePolicy,
    FileWatchPolicy WatchPolicy);
