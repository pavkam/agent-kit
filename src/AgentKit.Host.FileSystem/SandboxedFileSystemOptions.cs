// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Host.FileSystem;

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
}
