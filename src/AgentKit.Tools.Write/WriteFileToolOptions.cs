// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Write;

/// <summary>Configures file-system profile binding for <see cref="WriteFileTool"/>.</summary>
public sealed class WriteFileToolOptions
{
    /// <summary>Gets or sets the keyed profile used to select <see cref="IFileWriter"/>.</summary>
    public FileSystemProfileKey ProfileKey { get; set; } = new("workspace");

    /// <summary>Gets or sets the logical root identifier for workspace-relative paths.</summary>
    public FileRootId RootId { get; set; } = new("workspace");

    /// <summary>Gets or sets the absolute host directory backing <see cref="RootId"/>.</summary>
    public string HostRootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the security audience reported on file-write authorization requests.</summary>
    public ComponentId SecurityAudience { get; set; } = new("agentkit.filesystem.workspace");

    /// <summary>Gets or sets the lexical path policy applied before authorization.</summary>
    public FilePathPolicy PathPolicy { get; set; } = new(FilePathComparisonKind.Ordinal);
}
