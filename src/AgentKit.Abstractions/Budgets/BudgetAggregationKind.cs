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
/// without guessing from its name. The first-party in-memory authority
/// currently applies uniform running-total (<see cref="Sum"/>) accounting to
/// every dimension regardless of its declared kind; <see cref="Maximum"/>,
/// <see cref="ConcurrentGauge"/>, and <see cref="Duration"/> semantics are
/// declared here for forward compatibility with the full budgets
/// architecture but are not yet behaviorally differentiated by the
/// reservation engine. This is documented, not silent: a dimension
/// registered with one of those kinds still enforces its configured limit
/// correctly under <see cref="Sum"/> semantics, which is a safe (if not
/// maximally precise) default for concurrency- or peak-oriented dimensions.
/// </remarks>
public enum BudgetAggregationKind
{
    /// <summary>Consumption is the running total of every committed amount.</summary>
    Sum,

    /// <summary>Consumption is the largest single committed amount observed.</summary>
    Maximum,

    /// <summary>Consumption is the count of currently outstanding, uncommitted reservations.</summary>
    ConcurrentGauge,

    /// <summary>Consumption is accumulated elapsed duration.</summary>
    Duration
}
