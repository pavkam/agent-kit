// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of <see cref="BudgetLimit"/>, one configured ceiling captured with a scope.</summary>
/// <remarks>
/// The unit is persisted alongside the value and is never normalized, because the ledger refuses to convert between the
/// legal units of one dimension. Writing the ceiling without its unit would let a replayed scope silently enforce a
/// different quantity. <see cref="Kind"/> is written as its stable enumeration name, so reordering the enumeration cannot
/// turn an observational limit into an enforced one.
/// </remarks>
/// <param name="Dimension">The non-blank canonical dimension key this ceiling applies to.</param>
/// <param name="Value">The nonnegative configured ceiling.</param>
/// <param name="Unit">The non-blank canonical unit text <paramref name="Value"/> is expressed in.</param>
/// <param name="Kind">Whether the ceiling is enforced or observational.</param>
public sealed record JsonBudgetLimit(string Dimension, decimal Value, string Unit, BudgetLimitKind Kind)
{
    /// <summary>Projects one domain limit into its portable JSON representation.</summary>
    /// <param name="value">The non-null limit to project.</param>
    /// <returns>A document carrying the unwrapped dimension and unit text, the exact ceiling, and the limit kind.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetLimit FromDomain(BudgetLimit value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonBudgetLimit(value.Dimension.Value, value.Value, value.Unit.Value, value.Kind);
    }

    /// <summary>Reconstructs the exact domain limit this document was projected from.</summary>
    /// <returns>A limit equal to the projected original.</returns>
    /// <exception cref="ArgumentException">The persisted dimension or unit text is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The persisted value is negative or the persisted kind is undefined.</exception>
    public BudgetLimit ToDomain() => new(new BudgetDimension(Dimension), Value, new BudgetUnit(Unit), Kind);
}
