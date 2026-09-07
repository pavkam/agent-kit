// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Declares the aggregation semantics and legal units for one registered
/// <see cref="BudgetDimension"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Every
/// dimension a reservation or limit references must be registered through
/// exactly one descriptor; an unregistered dimension, or a reservation
/// expressed in a unit outside <see cref="AllowedUnits"/>, fails before it
/// can be misinterpreted.
/// </remarks>
public sealed record BudgetDimensionDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="BudgetDimensionDescriptor"/> record.</summary>
    /// <param name="dimension">The dimension this descriptor declares semantics for.</param>
    /// <param name="aggregation">How successive commitments against this dimension combine.</param>
    /// <param name="allowedUnits">The non-empty set of units a reservation against this dimension may use.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="aggregation"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="allowedUnits"/> is a default, uninitialized array, or is empty.
    /// </exception>
    public BudgetDimensionDescriptor(
        BudgetDimension dimension,
        BudgetAggregationKind aggregation,
        ImmutableArray<BudgetUnit> allowedUnits)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(aggregation);
        ArgumentException.ThrowIfDefault(allowedUnits);
        if (allowedUnits.IsEmpty)
        {
            throw new ArgumentException("At least one allowed unit is required.", nameof(allowedUnits));
        }

        Dimension = dimension;
        Aggregation = aggregation;
        AllowedUnits = allowedUnits;
    }

    /// <summary>Gets the dimension this descriptor declares semantics for.</summary>
    public BudgetDimension Dimension { get; init; }

    /// <summary>Gets how successive commitments against this dimension combine.</summary>
    public BudgetAggregationKind Aggregation { get; init; }

    /// <summary>Gets the set of units a reservation against this dimension may use.</summary>
    public ImmutableArray<BudgetUnit> AllowedUnits { get; init; }

    /// <inheritdoc/>
    public bool Equals(BudgetDimensionDescriptor? other) =>
        other is not null
        && Dimension.Equals(other.Dimension)
        && Aggregation == other.Aggregation
        && AllowedUnits.SequenceEqual(other.AllowedUnits);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Dimension);
        hash.Add(Aggregation);
        foreach (var unit in AllowedUnits)
        {
            hash.Add(unit);
        }

        return hash.ToHashCode();
    }
}
