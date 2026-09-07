// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.List;

/// <summary>Configures page bounds for the directory-listing tool.</summary>
public sealed class ListDirectoryToolOptions
{
    /// <summary>Gets or sets the page size used when the model omits <c>maximum_entries</c>.</summary>
    public int DefaultPageEntries { get; set; } = 200;

    /// <summary>Gets or sets the largest page the tool permits a model to request.</summary>
    public int MaximumPageEntries { get; set; } = 2_000;
}
