// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that proof of no usage released the previously started reservation.</summary>
public sealed record BudgetLedgerReconciliationReleased: BudgetLedgerReconciliationResult
{
    /// <summary>Initializes a reconciliation release receipt.</summary><param name="reservation">The non-null exact reservation released by proof.</param><exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public BudgetLedgerReconciliationReleased(BudgetLedgerReservationReference reservation) { ArgumentNullException.ThrowIfNull(reservation); Reservation = reservation; }
    /// <summary>Gets the released reservation locator.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
}
