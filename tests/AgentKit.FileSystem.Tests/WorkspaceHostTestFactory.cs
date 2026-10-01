// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem.Tests;

using Microsoft.Extensions.Logging;

/// <summary>Builds <see cref="OperatingSystemWorkspaceHost"/> instances over explicit roots and bounds without a service provider.</summary>
internal static class WorkspaceHostTestFactory
{
    /// <summary>The profile key every test host is bound to; it scopes <see cref="OperatingSystemWorkspaceHost.SecurityAudience"/>.</summary>
    internal static readonly FileSystemProfileKey ProfileKey = new("test");

    /// <summary>Creates a host over <paramref name="options"/>.</summary>
    /// <param name="options">The root and ceilings.</param>
    /// <param name="grantStore">The grant store consulted before every effect.</param>
    /// <param name="timeProvider">The clock used for search duration bounds.</param>
    /// <param name="logger">The optional logger.</param>
    /// <param name="intentIds">The optional enforcement-intent identity source; a GUID source by default.</param>
    /// <returns>A new host.</returns>
    internal static OperatingSystemWorkspaceHost Create(
        WorkspaceHostOptions options,
        ISecurityGrantStore grantStore,
        TimeProvider timeProvider,
        ILogger<OperatingSystemWorkspaceHost>? logger = null,
        IIdentifierGenerator<SecurityEnforcementIntentId>? intentIds = null) =>
        new(
            Snapshot(options),
            grantStore,
            timeProvider,
            logger,
            intentIds ?? new GuidSecurityEnforcementIntentIdGenerator());

    /// <summary>Projects test options onto the immutable profile snapshot the host consumes.</summary>
    /// <param name="options">The root and ceilings.</param>
    /// <returns>The snapshot.</returns>
    internal static OperatingSystemFileSystemOptionsSnapshot Snapshot(WorkspaceHostOptions options) => new(
        ProfileKey,
        new FileSystemProfileVersion(1),
        [new FileRootRegistration(new FileRootId("workspace"), options.RootDirectory)],
        new FilePathPolicy(FilePathComparisonKind.Ordinal),
        new FileSystemBounds(options.MaximumReadBytes, options.MaximumWriteBytes),
        new FileSystemWorkspaceBounds(
            options.MaximumDirectorySnapshotEntries,
            options.MaximumSearchDepth,
            options.MaximumSearchFiles,
            options.MaximumSearchBytes,
            options.MaximumSearchMatches,
            options.MaximumSearchLineBytes,
            options.MaximumSearchDuration,
            options.MaximumPatchEntries,
            options.MaximumPatchBytes),
        new FileWritePolicy(),
        new FileWatchPolicy());
}
