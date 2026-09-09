// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

using System.Diagnostics;

/// <summary>
/// The observed reserved and committed amounts for one
/// <see cref="BudgetDimension"/> within a scope, alongside its configured
/// limit, if any.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This is a
/// point-in-time observation; concurrent reservations may change these
/// values immediately after it is produced.
/// </remarks>
public sealed record BudgetDimensionUsage
{
    /// <summary>Initializes a new instance of the <see cref="BudgetDimensionUsage"/> record.</summary>
    /// <param name="dimension">The dimension this usage describes.</param>
    /// <param name="unit">The unit <paramref name="reserved"/> and <paramref name="committed"/> are expressed in.</param>
    /// <param name="reserved">The live reserved observation under the dimension's declared aggregation.</param>
    /// <param name="committed">The committed actual observation under the dimension's declared aggregation.</param>
    /// <param name="limit">The configured limit for this dimension, when one is configured.</param>
    public BudgetDimensionUsage(
        BudgetDimension dimension,
        BudgetUnit unit,
        BudgetQuantity reserved,
        BudgetQuantity committed,
        BudgetLimit? limit)
    {
        Dimension = dimension;
        Unit = unit;
        Reserved = reserved;
        Committed = committed;
        Limit = limit;
    }

    /// <summary>Initializes usage from nonnegative decimal-compatible quantities without rounding.</summary>
    /// <param name="dimension">The dimension this usage describes.</param>
    /// <param name="unit">The unit in which both quantities are expressed.</param>
    /// <param name="reserved">The nonnegative live reserved amount.</param>
    /// <param name="committed">The nonnegative committed amount.</param>
    /// <param name="limit">The configured limit, or <see langword="null"/> when none exists.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reserved"/> or <paramref name="committed"/> is negative.</exception>
    public BudgetDimensionUsage(BudgetDimension dimension, BudgetUnit unit, decimal reserved, decimal committed, BudgetLimit? limit)
        : this(dimension, unit, ToQuantity(reserved, nameof(reserved)), ToQuantity(committed, nameof(committed)), limit) { }

    /// <summary>Validates and exactly converts one compatibility decimal before record assignment.</summary>
    /// <param name="value">The nonnegative decimal quantity.</param>
    /// <param name="paramName">The public constructor parameter attributed to invalid input.</param>
    /// <returns>The exact canonical quantity.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative.</exception>
    private static BudgetQuantity ToQuantity(decimal value, string paramName)
    {
        Debug.Assert(!string.IsNullOrEmpty(paramName), "The constructor supplies its public decimal parameter name.");
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return BudgetQuantity.FromDecimal(value);
    }

    /// <summary>Gets the dimension this usage describes.</summary>
    public BudgetDimension Dimension { get; init; }

    /// <summary>Gets the unit <see cref="Reserved"/> and <see cref="Committed"/> are expressed in.</summary>
    public BudgetUnit Unit { get; init; }

    /// <summary>Gets live reserved usage: a sum for sum, duration, and concurrent-gauge dimensions, or the largest live amount for a maximum dimension.</summary>
    /// <value>The canonical exact nonnegative live reservation aggregate.</value>
    public BudgetQuantity Reserved { get; init; }

    /// <summary>Gets committed usage: a sum for sum and duration dimensions, the largest actual for a maximum dimension, or zero for a concurrent gauge.</summary>
    /// <value>The canonical exact nonnegative committed aggregate.</value>
    public BudgetQuantity Committed { get; init; }

    /// <summary>Gets the configured limit for this dimension, when one is configured.</summary>
    public BudgetLimit? Limit { get; init; }
}
