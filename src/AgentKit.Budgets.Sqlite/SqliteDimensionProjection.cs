// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Represents one exact indexed dimension projection used for bounded admission and snapshots.</summary>
internal sealed record SqliteDimensionProjection
{
    /// <summary>Creates a validated projection decoded from storage.</summary>
    /// <param name="dimension">The nonblank dimension.</param><param name="unit">The nonblank fixed unit.</param>
    /// <param name="aggregation">The defined captured aggregation.</param><param name="reserved">The exact live capacity.</param>
    /// <param name="committed">The exact committed usage.</param><param name="openCount">The nonnegative live row count.</param>
    /// <exception cref="ArgumentException">A dimension or unit is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="aggregation"/> is undefined or <paramref name="openCount"/> is negative.</exception>
    internal SqliteDimensionProjection(BudgetDimension dimension, BudgetUnit unit, BudgetAggregationKind aggregation, BudgetQuantity reserved, BudgetQuantity committed, int openCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension.Value, nameof(dimension));
        ArgumentException.ThrowIfNullOrWhiteSpace(unit.Value, nameof(unit));
        ArgumentOutOfRangeException.ThrowIfUndefined(aggregation);
        ArgumentOutOfRangeException.ThrowIfNegative(openCount);
        Dimension = dimension;
        Unit = unit;
        Aggregation = aggregation;
        Reserved = reserved;
        Committed = committed;
        OpenCount = openCount;
    }

    /// <summary>Gets the dimension.</summary><value>The nonblank dimension key.</value>
    internal BudgetDimension Dimension { get; }
    /// <summary>Gets the fixed unit.</summary><value>The nonblank unit.</value>
    internal BudgetUnit Unit { get; }
    /// <summary>Gets aggregation semantics.</summary><value>The defined captured kind.</value>
    internal BudgetAggregationKind Aggregation { get; }
    /// <summary>Gets exact retained capacity.</summary><value>The nonnegative live projection.</value>
    internal BudgetQuantity Reserved { get; }
    /// <summary>Gets exact committed usage.</summary><value>The nonnegative committed projection.</value>
    internal BudgetQuantity Committed { get; }
    /// <summary>Gets live row count.</summary><value>The nonnegative count used by captured admission.</value>
    internal int OpenCount { get; }
}
