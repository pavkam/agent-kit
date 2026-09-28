// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Capability flags advertised by one compaction strategy.</summary>
[Flags]
public enum CompactionStrategyCapabilities
{
    /// <summary>No capability flags are set.</summary>
    None = 0,

    /// <summary>The strategy produces deterministic extractive checkpoints.</summary>
    Extractive = 1,

    /// <summary>The strategy produces semantic summaries.</summary>
    SemanticSummary = 2,

    /// <summary>The strategy can emit structured state references.</summary>
    StructuredState = 4,

    /// <summary>The strategy can repair an oversized turn prefix.</summary>
    OversizedTurnRepair = 8,
}
