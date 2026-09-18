// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of <see cref="BudgetScopeAdmission"/>, the capacity policy frozen when a scope was admitted.</summary>
/// <remarks>
/// Admission facts are persisted with their scope precisely so a later options change cannot retroactively widen or narrow
/// an existing scope. Replaying them from the journal therefore restores the bounds the scope was created under, not the
/// bounds the recovering process happens to be configured with.
/// </remarks>
/// <param name="MaximumScopeDepth">The positive maximum number of scopes in the lineage including the admitted scope.</param>
/// <param name="MaximumOpenReservationsPerScope">The positive maximum number of capacity-retaining reservations per scope.</param>
/// <param name="DefaultReservationLifetime">The positive lifetime added to the ledger clock when an original request omits an expiry.</param>
/// <param name="OverrunHoldPolicy">The defined policy applied to overrun generations owned by the admitted boundary.</param>
public sealed record JsonBudgetScopeAdmission(
    int MaximumScopeDepth,
    int MaximumOpenReservationsPerScope,
    TimeSpan DefaultReservationLifetime,
    BudgetOverrunHoldPolicy OverrunHoldPolicy)
{
    /// <summary>Projects one domain admission record into its portable JSON representation.</summary>
    /// <param name="value">The non-null captured admission facts.</param>
    /// <returns>A document carrying every captured bound and the boundary's overrun policy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetScopeAdmission FromDomain(BudgetScopeAdmission value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonBudgetScopeAdmission(
            value.MaximumScopeDepth,
            value.MaximumOpenReservationsPerScope,
            value.DefaultReservationLifetime,
            value.OverrunHoldPolicy);
    }

    /// <summary>Reconstructs the exact domain admission record this document was projected from.</summary>
    /// <returns>Admission facts equal to the projected original.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A persisted bound is not positive or the persisted overrun policy is undefined.</exception>
    public BudgetScopeAdmission ToDomain() => new(
        MaximumScopeDepth, MaximumOpenReservationsPerScope, DefaultReservationLifetime, OverrunHoldPolicy);
}
