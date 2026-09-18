// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Portable JSON mirror of <see cref="BudgetOverrunHoldReference"/>, locating one boundary-specific overrun generation.</summary>
/// <remarks>
/// The domain reference also carries the boundary and reservation addresses, but those are already authoritative in the
/// replayed projection, so this document stores only the two identities and the generation. Rebuilding the addresses from
/// the projection rather than from the journal line keeps a hand-edited record from claiming a hold at an address the
/// scope never had.
/// </remarks>
/// <param name="BoundaryScopeId">The raw value of the non-empty scope boundary that owns the generation.</param>
/// <param name="ReservationId">The raw value of the non-empty reservation whose accounting crossed into overrun.</param>
/// <param name="TriggeringRevision">The positive accounting revision that created the generation.</param>
public sealed record JsonBudgetOverrunHoldReference(
    Guid BoundaryScopeId,
    Guid ReservationId,
    long TriggeringRevision)
{
    /// <summary>Projects one domain hold reference into its portable JSON representation.</summary>
    /// <param name="value">The non-null hold generation locator to project.</param>
    /// <returns>A document carrying the boundary identity, the triggering reservation identity, and the generation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static JsonBudgetOverrunHoldReference FromDomain(BudgetOverrunHoldReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new JsonBudgetOverrunHoldReference(
            value.Boundary.Id.Value, value.Reservation.Id.Value, value.TriggeringRevision.Value);
    }

    /// <summary>Reconstructs the exact domain hold reference using addresses resolved from the replayed projection.</summary>
    /// <param name="boundary">The non-null replayed boundary reference whose identity must match <see cref="BoundaryScopeId"/>.</param>
    /// <param name="reservation">The non-null replayed reservation reference whose identity must match <see cref="ReservationId"/>.</param>
    /// <returns>A hold reference equal to the projected original.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="boundary"/> or <paramref name="reservation"/> is null.</exception>
    /// <exception cref="ArgumentException">A supplied reference does not carry the persisted identity, or the reservation address is not within the boundary lineage.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="TriggeringRevision"/> is not positive.</exception>
    public BudgetOverrunHoldReference ToDomain(
        BudgetLedgerScopeReference boundary, BudgetLedgerReservationReference reservation)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentException.ThrowIfNotEqual(boundary.Id.Value, BoundaryScopeId, nameof(boundary));
        ArgumentException.ThrowIfNotEqual(reservation.Id.Value, ReservationId, nameof(reservation));
        return new BudgetOverrunHoldReference(boundary, reservation, new BudgetAccountingRevision(TriggeringRevision));
    }
}
