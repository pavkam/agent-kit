// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Stable first-party compaction strategy keys.</summary>
/// <remarks>
/// Each key is both the keyed registration identity under a compactor and the strategy key recorded as provenance in the
/// checkpoint's <see cref="CompactionProducer"/>. Versions are carried by the strategy descriptor, never by the key.
/// </remarks>
public static class CompactionStrategyKeys
{
    /// <summary>Gets the key of the deterministic <see cref="ExtractiveCompactionStrategy"/>.</summary>
    public static CompactionStrategyKey Extractive { get; } = new("agentkit.extractive");

    /// <summary>Gets the key of the model-written <see cref="ModelCompactionStrategy"/>.</summary>
    public static CompactionStrategyKey ModelSummary { get; } = new("agentkit.model-summary");
}
