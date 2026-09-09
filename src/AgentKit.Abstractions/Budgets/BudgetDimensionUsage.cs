// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="reserved"/> or <paramref name="committed"/> is negative.
    /// </exception>
    public BudgetDimensionUsage(
        BudgetDimension dimension,
        BudgetUnit unit,
        decimal reserved,
        decimal committed,
        BudgetLimit? limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reserved);
        ArgumentOutOfRangeException.ThrowIfNegative(committed);

        Dimension = dimension;
        Unit = unit;
        Reserved = reserved;
        Committed = committed;
        Limit = limit;
    }

    /// <summary>Gets the dimension this usage describes.</summary>
    public BudgetDimension Dimension { get; init; }

    /// <summary>Gets the unit <see cref="Reserved"/> and <see cref="Committed"/> are expressed in.</summary>
    public BudgetUnit Unit { get; init; }

    /// <summary>Gets live reserved usage: a sum for sum, duration, and concurrent-gauge dimensions, or the largest live amount for a maximum dimension.</summary>
    public decimal Reserved { get; init; }

    /// <summary>Gets committed usage: a sum for sum and duration dimensions, the largest actual for a maximum dimension, or zero for a concurrent gauge.</summary>
    public decimal Committed { get; init; }

    /// <summary>Gets the configured limit for this dimension, when one is configured.</summary>
    public BudgetLimit? Limit { get; init; }
}
