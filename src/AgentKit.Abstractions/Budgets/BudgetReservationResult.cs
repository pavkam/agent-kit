// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for the terminal outcome of one
/// <see cref="BudgetReservationRequest"/>.
/// </summary>
/// <remarks>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="BudgetReserved"/> and <see cref="BudgetRejected"/>. Its
/// constructor is <see langword="private protected"/>, so no assembly
/// outside AgentKit.Abstractions can add a third kind.
/// </remarks>
public abstract record BudgetReservationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BudgetReservationResult"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    private protected BudgetReservationResult()
    {
    }
}
