// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.FileSystem;

/// <summary>Configures the root-jailed <see cref="SandboxedFileSystem"/>.</summary>
/// <remarks>
/// This is a mutable options type bound through <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>;
/// it is configured once at composition time and treated as read-only by
/// every consumer afterward.
/// </remarks>
public sealed class SandboxedFileSystemOptions
{
    /// <summary>
    /// Gets or sets the absolute root directory every <see cref="FileSystemPath"/>
    /// is resolved relative to. Every read and write is re-validated to
    /// stay within this directory, regardless of any higher-level
    /// authorization already performed.
    /// </summary>
    /// <value>Must be set to an absolute path before use; there is no default.</value>
    public string RootDirectory { get; set; } = "";

    /// <summary>Gets or sets the maximum number of bytes a single read may return.</summary>
    /// <value>Defaults to 10 MiB.</value>
    public long MaximumReadBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the maximum number of bytes a single write may write.</summary>
    /// <value>Defaults to 10 MiB.</value>
    public long MaximumWriteBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Gets or sets the maximum child names retained while producing one stable directory snapshot.</summary>
    /// <value>Defaults to 10,000 entries and must be positive.</value>
    public int MaximumDirectorySnapshotEntries { get; set; } = 10_000;

    /// <summary>Gets or sets the maximum recursive depth accepted for one content search.</summary>
    /// <value>Defaults to 100 and must be positive.</value>
    public int MaximumSearchDepth { get; set; } = 100;

    /// <summary>Gets or sets the maximum candidate files accepted for one content search.</summary>
    /// <value>Defaults to 10,000 and must be positive.</value>
    public int MaximumSearchFiles { get; set; } = 10_000;

    /// <summary>Gets or sets the maximum total file bytes accepted for one content search.</summary>
    /// <value>Defaults to 100 MiB and must be positive.</value>
    public long MaximumSearchBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>Gets or sets the maximum retained matches accepted for one content search.</summary>
    /// <value>Defaults to 10,000 and must be positive.</value>
    public int MaximumSearchMatches { get; set; } = 10_000;

    /// <summary>Gets or sets the maximum retained line bytes accepted for one content search.</summary>
    /// <value>Defaults to 64 KiB and must be positive.</value>
    public int MaximumSearchLineBytes { get; set; } = 64 * 1024;

    /// <summary>Gets or sets the maximum elapsed duration accepted for one content search.</summary>
    /// <value>Defaults to one minute and must be positive.</value>
    public TimeSpan MaximumSearchDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the maximum entries accepted in one workspace patch.</summary>
    /// <value>Defaults to 100 and must be positive.</value>
    public int MaximumPatchEntries { get; set; } = 100;

    /// <summary>Gets or sets the maximum aggregate staged bytes accepted in one workspace patch.</summary>
    /// <value>Defaults to 50 MiB and must be positive.</value>
    public long MaximumPatchBytes { get; set; } = 50 * 1024 * 1024;
}
