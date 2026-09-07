// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Configures query, provider-attempt, and model-projection bounds.</summary>
public sealed class WebSearchToolOptions
{
    /// <summary>Gets or sets the maximum query characters.</summary>
    public int MaximumQueryCharacters { get; set; } = 1_000;
    /// <summary>Gets or sets the maximum domain filters.</summary>
    public int MaximumDomains { get; set; } = 10;
    /// <summary>Gets or sets the default result ceiling.</summary>
    public int DefaultMaximumResults { get; set; } = 5;
    /// <summary>Gets or sets the hard result ceiling.</summary>
    public int MaximumResults { get; set; } = 10;
    /// <summary>Gets or sets the default provider-attempt timeout.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Gets or sets the largest provider-attempt timeout.</summary>
    public TimeSpan MaximumTimeout { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Gets or sets the maximum projected title characters per result.</summary>
    public int MaximumTitleCharacters { get; set; } = 300;
    /// <summary>Gets or sets the maximum projected snippet characters per result.</summary>
    public int MaximumSnippetCharacters { get; set; } = 2_000;
}
