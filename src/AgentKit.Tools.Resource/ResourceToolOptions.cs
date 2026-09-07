// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Resource;

/// <summary>Configures the immutable resource catalog and model-facing read bounds.</summary>
public sealed class ResourceToolOptions
{
    /// <summary>Gets the host-configured resources captured when the tool is constructed.</summary>
    public IList<FileResourceDefinition> Resources { get; } = [];

    /// <summary>Gets or sets the maximum complete backing-file bytes.</summary>
    public long MaximumBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum retained decoded characters.</summary>
    public int MaximumCharacters { get; set; } = 200_000;

    /// <summary>Gets or sets the maximum host-authored description characters.</summary>
    public int MaximumDescriptionCharacters { get; set; } = 2_000;
}
