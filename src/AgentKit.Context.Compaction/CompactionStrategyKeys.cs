// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Stable first-party compaction strategy keys.</summary>
public static class CompactionStrategyKeys
{
    /// <summary>Gets the extractive strategy key.</summary>
    public static CompactionStrategyKey Extractive { get; } = new("agentkit.extractive");
}
