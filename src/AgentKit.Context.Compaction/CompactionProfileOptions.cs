// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Mutable compaction profile options captured at registration time.</summary>
public sealed class CompactionProfileOptions
{
    /// <summary>Gets or sets the profile version.</summary>
    public CompactionProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets or sets a value indicating whether compaction is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets the ordered strategy keys.</summary>
    public List<CompactionStrategyKey> StrategyOrder { get; set; } = [CompactionStrategyKeys.Extractive];

    /// <summary>Gets or sets a value indicating whether oversized-turn repair is allowed.</summary>
    public bool AllowOversizedTurnRepair { get; set; }
}
