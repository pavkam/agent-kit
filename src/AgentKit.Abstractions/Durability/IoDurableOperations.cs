// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names and versions the recoverable operations input admission and run settlement can journal.</summary>
/// <remarks>
/// A durability profile enables operations by name, so these values are part of an application's configuration
/// surface: a profile that does not list a name leaves that boundary undurable rather than silently journaling it.
/// The versions are bumped whenever the encoded manifest shape changes, because recovery refuses a payload version it
/// cannot read rather than reinterpreting old bytes under new rules.
/// </remarks>
public static class IoDurableOperations
{
    /// <summary>Gets the operation name for promoting queued input at one safe loop boundary.</summary>
    /// <value>The name a durability profile must enable before input promotions are journaled.</value>
    public static DurableOperationName InputPromotion { get; } = new("agentkit.io.input_promotion");

    /// <summary>Gets the operation name for one run's terminal settlement.</summary>
    /// <value>The name a durability profile must enable before run settlement is journaled.</value>
    public static DurableOperationName RunSettlement { get; } = new("agentkit.io.run_settlement");

    /// <summary>Gets the published version of the input-promotion manifest shape.</summary>
    /// <value>The version recorded on every input-promotion declaration.</value>
    public static DurableOperationVersion InputPromotionVersion { get; } = new("v1");

    /// <summary>Gets the published version of the run-settlement manifest shape.</summary>
    /// <value>The version recorded on every run-settlement declaration.</value>
    public static DurableOperationVersion RunSettlementVersion { get; } = new("v1");

    /// <summary>Gets every operation name these boundaries can journal.</summary>
    /// <value>The complete additive set a profile may enable; no other name is journaled by input or output.</value>
    public static ImmutableArray<DurableOperationName> All { get; } = [InputPromotion, RunSettlement];
}
