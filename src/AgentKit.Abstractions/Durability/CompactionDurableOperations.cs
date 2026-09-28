// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names and versions the recoverable operation context compaction can journal.</summary>
/// <remarks>
/// Only activation is journaled. Cut selection, summary generation, and validation compute a candidate without
/// changing durable session truth, so replaying them costs work but never produces a second committed record;
/// activation is the one boundary where a crash leaves the question of whether the record was appended.
/// </remarks>
public static class CompactionDurableOperations
{
    /// <summary>Gets the operation name for appending one validated compaction record to the session.</summary>
    /// <value>The name a durability profile must enable before compaction activation is journaled.</value>
    public static DurableOperationName Activation { get; } = new("agentkit.compaction.activation");

    /// <summary>Gets the published version of the compaction-activation manifest shape.</summary>
    /// <value>The version recorded on every compaction-activation declaration.</value>
    public static DurableOperationVersion ActivationVersion { get; } = new("v1");

    /// <summary>Gets every operation name compaction can journal.</summary>
    /// <value>The complete additive set a profile may enable; compaction journals no other name.</value>
    public static ImmutableArray<DurableOperationName> All { get; } = [Activation];
}
