// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Read;

/// <summary>Configures line windows and file-system profile binding for <see cref="ReadFileTool"/>.</summary>
/// <remarks>
/// An omitted <c>limit</c> never means unbounded: the tool returns at most
/// <see cref="DefaultMaximumLines"/> logical lines. Byte bounds come from
/// <see cref="MaximumReadBytes"/> and the selected <see cref="IFileReader"/> profile.
/// </remarks>
public sealed class ReadFileToolOptions
{
    /// <summary>Gets or sets the keyed profile used to select <see cref="IFileReader"/>.</summary>
    public FileSystemProfileKey ProfileKey { get; set; } = new("workspace");

    /// <summary>Gets or sets the logical root identifier for workspace-relative paths.</summary>
    public FileRootId RootId { get; set; } = new("workspace");

    /// <summary>Gets or sets the absolute host directory backing <see cref="RootId"/>.</summary>
    public string HostRootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the security audience reported on file-read authorization requests.</summary>
    public ComponentId SecurityAudience { get; set; } = new("agentkit.filesystem.workspace");

    /// <summary>Gets or sets the maximum bytes authorized for one read request.</summary>
    public long MaximumReadBytes { get; set; } = 10_485_760;

    /// <summary>Gets or sets the lexical path policy applied before authorization.</summary>
    public FilePathPolicy PathPolicy { get; set; } = new(FilePathComparisonKind.Ordinal);

    /// <summary>Gets or sets the number of lines returned when the model omits <c>limit</c>.</summary>
    public int DefaultMaximumLines { get; set; } = 2_000;

    /// <summary>Gets or sets the largest <c>limit</c> the tool permits a model to request.</summary>
    public int MaximumLines { get; set; } = 20_000;
}
