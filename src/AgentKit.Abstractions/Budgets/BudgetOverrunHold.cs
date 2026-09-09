// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports one immutable boundary-specific hold generation and its current accounting evidence.</summary>
public sealed record BudgetOverrunHold
{
    /// <summary>Creates hold evidence.</summary><param name="reference">The exact generation.</param><param name="dimension">The charged dimension.</param><param name="unit">The charged unit.</param><param name="reserved">The original row amount.</param><param name="currentActual">The current truthful actual.</param><param name="policy">The owning boundary's captured policy.</param><exception cref="ArgumentNullException"><paramref name="reference"/> is null.</exception><exception cref="ArgumentException"><paramref name="dimension"/> or <paramref name="unit"/> is default.</exception><exception cref="ArgumentOutOfRangeException">An amount is negative or <paramref name="policy"/> is undefined.</exception>
    public BudgetOverrunHold(BudgetOverrunHoldReference reference, BudgetDimension dimension, BudgetUnit unit, decimal reserved, decimal currentActual, BudgetOverrunHoldPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(reference); ArgumentException.ThrowIfNullOrWhiteSpace(dimension.Value, nameof(dimension)); ArgumentException.ThrowIfNullOrWhiteSpace(unit.Value, nameof(unit)); ArgumentOutOfRangeException.ThrowIfNegative(reserved); ArgumentOutOfRangeException.ThrowIfNegative(currentActual); ArgumentOutOfRangeException.ThrowIfUndefined(policy);
        Reference = reference; Dimension = dimension; Unit = unit; Reserved = reserved; CurrentActual = currentActual; Policy = policy;
    }
    /// <summary>Gets the generation locator.</summary><value>The exact boundary, row, and revision.</value>
    public BudgetOverrunHoldReference Reference { get; }
    /// <summary>Gets the charged dimension.</summary><value>The original dimension.</value>
    public BudgetDimension Dimension { get; }
    /// <summary>Gets the charged unit.</summary><value>The original unit.</value>
    public BudgetUnit Unit { get; }
    /// <summary>Gets the reserved row amount.</summary><value>A nonnegative decimal.</value>
    public decimal Reserved { get; }
    /// <summary>Gets the current actual row amount.</summary><value>A nonnegative truthful decimal.</value>
    public decimal CurrentActual { get; }
    /// <summary>Gets the boundary policy.</summary><value>The policy captured when that boundary was admitted.</value>
    public BudgetOverrunHoldPolicy Policy { get; }
}
