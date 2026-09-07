// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Glob;

/// <summary>Configures default and absolute traversal bounds for the glob tool.</summary>
public sealed class GlobToolOptions
{
    /// <summary>Gets or sets the default recursive depth.</summary>
    public int DefaultMaximumDepth { get; set; } = 25;
    /// <summary>Gets or sets the host-configured depth ceiling.</summary>
    public int MaximumDepth { get; set; } = 100;
    /// <summary>Gets or sets the default observed-name limit.</summary>
    public int DefaultMaximumVisitedEntries { get; set; } = 10_000;
    /// <summary>Gets or sets the host-configured observed-name ceiling.</summary>
    public int MaximumVisitedEntries { get; set; } = 100_000;
    /// <summary>Gets or sets the default retained-match limit.</summary>
    public int DefaultMaximumResults { get; set; } = 1_000;
    /// <summary>Gets or sets the host-configured retained-match ceiling.</summary>
    public int MaximumResults { get; set; } = 10_000;
}
