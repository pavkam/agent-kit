// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A configured ceiling for one <see cref="BudgetDimension"/> at a specific
/// <see cref="BudgetUnit"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization.
/// </remarks>
public sealed record BudgetLimit
{
    /// <summary>Initializes a new instance of the <see cref="BudgetLimit"/> record.</summary>
    /// <param name="dimension">The dimension this limit applies to.</param>
    /// <param name="value">The non-negative configured ceiling value.</param>
    /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
    /// <param name="kind">Whether this limit is enforced or observational.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative, or <paramref name="kind"/> is undefined.
    /// </exception>
    public BudgetLimit(BudgetDimension dimension, decimal value, BudgetUnit unit, BudgetLimitKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);

        Dimension = dimension;
        Value = value;
        Unit = unit;
        Kind = kind;
    }

    /// <summary>Gets the dimension this limit applies to.</summary>
    public BudgetDimension Dimension { get; init; }

    /// <summary>Gets the configured ceiling value.</summary>
    public decimal Value { get; init; }

    /// <summary>Gets the unit <see cref="Value"/> is expressed in.</summary>
    public BudgetUnit Unit { get; init; }

    /// <summary>Gets whether this limit is enforced or observational.</summary>
    public BudgetLimitKind Kind { get; init; }
}
