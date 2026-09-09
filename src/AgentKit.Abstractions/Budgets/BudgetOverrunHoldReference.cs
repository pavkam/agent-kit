// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Locates one boundary-specific overrun generation without an unrelated generated identity.</summary>
public sealed record BudgetOverrunHoldReference
{
    /// <summary>Creates an exact hold locator.</summary><param name="boundary">The exact owning boundary.</param><param name="reservation">The triggering reservation.</param><param name="triggeringRevision">The accounting revision that crossed into overrun.</param><exception cref="ArgumentNullException"><paramref name="boundary"/> or <paramref name="reservation"/> is null.</exception><exception cref="ArgumentException">The reservation address differs from the boundary lineage address.</exception><exception cref="ArgumentOutOfRangeException"><paramref name="triggeringRevision"/> is default.</exception>
    public BudgetOverrunHoldReference(BudgetLedgerScopeReference boundary, BudgetLedgerReservationReference reservation, BudgetAccountingRevision triggeringRevision)
    {
        ArgumentNullException.ThrowIfNull(boundary); ArgumentNullException.ThrowIfNull(reservation); ArgumentOutOfRangeException.ThrowIfEqual(triggeringRevision, default);
        ArgumentException.ThrowIfNotBudgetScopeAncestorAddress(boundary.Address, reservation.Scope.Address, nameof(reservation));
        Boundary = boundary; Reservation = reservation; TriggeringRevision = triggeringRevision;
    }
    /// <summary>Gets the owning boundary.</summary><value>The exact persisted scope reference.</value>
    public BudgetLedgerScopeReference Boundary { get; }
    /// <summary>Gets the triggering reservation.</summary><value>The exact persisted reservation reference.</value>
    public BudgetLedgerReservationReference Reservation { get; }
    /// <summary>Gets the accounting generation.</summary><value>The positive revision that created this generation.</value>
    public BudgetAccountingRevision TriggeringRevision { get; }
}
