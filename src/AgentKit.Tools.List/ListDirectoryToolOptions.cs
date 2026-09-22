// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

/// <summary>Configures page bounds and keyed host profile binding for the directory-listing tool.</summary>
public sealed class ListDirectoryToolOptions
{
    /// <summary>Gets or sets the profile key selecting the host file-system capabilities.</summary>
    public FileSystemProfileKey ProfileKey { get; set; } = new("workspace");

    /// <summary>Gets or sets the logical root identifier bound to authorization resources.</summary>
    public FileRootId RootId { get; set; } = new("workspace");

    /// <summary>Gets or sets the absolute host path backing <see cref="RootId"/>.</summary>
    public string HostRootPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the lexical path policy applied before authorization.</summary>
    public FilePathPolicy PathPolicy { get; set; } = new(FilePathComparisonKind.Ordinal);

    /// <summary>Gets or sets the page size used when the model omits <c>maximum_entries</c>.</summary>
    public int DefaultPageEntries { get; set; } = 200;

    /// <summary>Gets or sets the largest page the tool permits a model to request.</summary>
    public int MaximumPageEntries { get; set; } = 2_000;
}
