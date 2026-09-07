// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>
/// Holds one scope's mutable in-memory accounting for a single budget
/// dimension while the authority-owned hierarchy gate is held.
/// </summary>
/// <param name="unit">The one unit accepted for this dimension's accounting.</param>
internal sealed class InMemoryBudgetDimensionState(BudgetUnit unit)
{
    /// <summary>Gets the unit shared by reserved and committed amounts.</summary>
    internal BudgetUnit Unit { get; } = unit;

    /// <summary>Gets or sets capacity retained by open or unresolved started reservations.</summary>
    internal decimal Reserved { get; set; }

    /// <summary>Gets or sets settled actual usage after applied corrections.</summary>
    internal decimal Committed { get; set; }

    /// <summary>Gets or sets whether truthful overrun accounting prevents new admission.</summary>
    internal bool Blocked { get; set; }

    /// <summary>Gets reservations that still retain capacity, including unresolved started work.</summary>
    internal List<InMemoryBudgetReservation> Open { get; } = [];

    /// <summary>Gets settled reservations retained so corrections can recompute overrun blocking.</summary>
    internal List<InMemoryBudgetReservation> CommittedReservations { get; } = [];
}
