// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project;

/// <summary>Discovery bounds and file-system profile binding for workspace project instruction files.</summary>
public sealed class ProjectInstructionOptions
{
    /// <summary>Gets or sets the keyed file-system profile that every instruction read selects through <see cref="IFileSystemSelector"/>.</summary>
    /// <value>A profile key whose registration supports <see cref="FileSystemCapability.Read"/>; default <c>workspace</c>.</value>
    public FileSystemProfileKey ProfileKey { get; set; } = new("workspace");

    /// <summary>Gets or sets the logical root identity that instruction targets are resolved under.</summary>
    /// <value>The root identity bound into every read request; default <c>workspace</c>.</value>
    public FileRootId RootId { get; set; } = new("workspace");

    /// <summary>Gets or sets the absolute host directory that backs <see cref="RootId"/>.</summary>
    /// <value>An absolute host path. It is an external fact of the deployment, so there is no default and composition validation rejects a blank value.</value>
    public string HostRootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets relative search roots scanned for instruction filenames.</summary>
    /// <value>Nonblank relative paths; default scans the workspace root only.</value>
    public string[] SearchRoots { get; set; } = ["."];

    /// <summary>Gets or sets instruction filenames discovered under each search root.</summary>
    /// <value>Nonblank filenames compared case-sensitively against the host file-system boundary.</value>
    public string[] InstructionFilenames { get; set; } = ["AGENTS.md", "CLAUDE.md"];

    /// <summary>Gets or sets the maximum bytes read from one discovered instruction file.</summary>
    /// <value>A positive byte count; files whose host length exceeds it are skipped rather than truncated.</value>
    public int MaxBytesPerFile { get; set; } = 256_000;
}
