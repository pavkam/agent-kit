// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares how successive commitments against one <see cref="BudgetDimension"/>
/// combine into its observed consumption.
/// </summary>
/// <remarks>
/// This descriptor value travels with a <see cref="BudgetDimensionDescriptor"/>
/// so every consumer agrees on what "consumption" means for a dimension
/// without guessing from its name. Reservation amounts always remain expressed
/// in the descriptor's declared unit: a concurrent-gauge reservation for three
/// slots therefore consumes the same live capacity as three one-slot
/// reservations. Implementations must apply the selected aggregation exactly;
/// silently substituting running-total semantics changes capacity and is not a
/// compatible fallback.
/// </remarks>
public enum BudgetAggregationKind
{
    /// <summary>Consumption is the running total of every committed amount.</summary>
    Sum,

    /// <summary>Consumption is the maximum of live reserved amounts and committed actual amounts.</summary>
    Maximum,

    /// <summary>Consumption is the sum of live capacity-retaining amounts and becomes zero after proven completion or release.</summary>
    ConcurrentGauge,

    /// <summary>Consumption is accumulated elapsed duration.</summary>
    Duration
}
