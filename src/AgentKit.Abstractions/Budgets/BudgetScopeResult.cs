// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one
/// <see cref="BudgetScopeRequest"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="BudgetScopeCreated"/> and
/// <see cref="BudgetScopeCreationFailed"/>. Valid instances are restricted
/// to those built-in variants: the parameterless constructor cannot be used
/// outside this assembly, and the guarded record-copy constructor prevents
/// an external derived record from bootstrapping itself from a built-in
/// variant.
/// </remarks>
public abstract record BudgetScopeResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetScopeResult"/>
    /// record without copying an existing result.
    /// </summary>
    /// <remarks>
    /// The private-protected accessibility admits direct construction only by
    /// variants declared in this assembly.
    /// </remarks>
    private protected BudgetScopeResult()
    {
    }

    /// <summary>
    /// Copies a result only when the source is the same closed concrete
    /// variant as the value under construction.
    /// </summary>
    /// <param name="original">The nonnull same-variant result to copy.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="original"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="original"/> has a different concrete runtime type.
    /// </exception>
    /// <remarks>
    /// Record inheritance requires a protected copy constructor. This guard
    /// preserves the closed result family by preventing an external derived
    /// record from bootstrapping itself by copying one of its built-in
    /// variants, while allowing normal copies of a legitimate same-variant
    /// result.
    /// </remarks>
    protected BudgetScopeResult(BudgetScopeResult original)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentException.ThrowIfNotEqual(original.GetType(), GetType(), nameof(original));
    }
}
