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
    /// <exception cref="ArgumentException"><paramref name="dimension"/> or <paramref name="unit"/> is default or blank.</exception>
    public BudgetLimit(BudgetDimension dimension, decimal value, BudgetUnit unit, BudgetLimitKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension.Value, nameof(dimension));
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit.Value, nameof(unit));
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);

        Dimension = dimension;
        Value = value;
        Unit = unit;
        Kind = kind;
    }

    /// <summary>Gets the dimension this limit applies to.</summary>
    /// <exception cref="ArgumentException">An initializer assigns a default or blank dimension.</exception>
    public BudgetDimension Dimension
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(Dimension));
            field = value;
        }
    }

    /// <summary>Gets the configured ceiling value.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns a negative value.</exception>
    public decimal Value
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(Value));
            field = value;
        }
    }

    /// <summary>Gets the unit <see cref="Value"/> is expressed in.</summary>
    /// <exception cref="ArgumentException">An initializer assigns a default or blank unit.</exception>
    public BudgetUnit Unit
    {
        get;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value.Value, nameof(Unit));
            field = value;
        }
    }

    /// <summary>Gets whether this limit is enforced or observational.</summary>
    /// <exception cref="ArgumentOutOfRangeException">An initializer assigns an undefined value.</exception>
    public BudgetLimitKind Kind
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfUndefined(value, nameof(Kind));
            field = value;
        }
    }
}
