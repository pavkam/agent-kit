// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Search;

/// <summary>Configures default and absolute bounds for the content-search tool.</summary>
public sealed class SearchToolOptions
{
    /// <summary>Gets or sets the default traversal depth.</summary>
    public int DefaultMaximumDepth { get; set; } = 25;
    /// <summary>Gets or sets the traversal-depth ceiling.</summary>
    public int MaximumDepth { get; set; } = 100;
    /// <summary>Gets or sets the default candidate-file bound.</summary>
    public int DefaultMaximumFiles { get; set; } = 1_000;
    /// <summary>Gets or sets the candidate-file ceiling.</summary>
    public int MaximumFiles { get; set; } = 10_000;
    /// <summary>Gets or sets the default observed-byte bound.</summary>
    public long DefaultMaximumBytes { get; set; } = 10 * 1024 * 1024;
    /// <summary>Gets or sets the observed-byte ceiling.</summary>
    public long MaximumBytes { get; set; } = 100 * 1024 * 1024;
    /// <summary>Gets or sets the default retained-match bound.</summary>
    public int DefaultMaximumMatches { get; set; } = 1_000;
    /// <summary>Gets or sets the retained-match ceiling.</summary>
    public int MaximumMatches { get; set; } = 10_000;
    /// <summary>Gets or sets the default retained bytes per matching line.</summary>
    public int DefaultMaximumLineBytes { get; set; } = 4 * 1024;
    /// <summary>Gets or sets the retained line-byte ceiling.</summary>
    public int MaximumLineBytes { get; set; } = 64 * 1024;
    /// <summary>Gets or sets the default elapsed search duration.</summary>
    public TimeSpan DefaultMaximumDuration { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>Gets or sets the elapsed-duration ceiling.</summary>
    public TimeSpan MaximumDuration { get; set; } = TimeSpan.FromMinutes(1);
}
