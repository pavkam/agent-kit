// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that unstarted capacity was released or was already released by an identical transition.</summary>
public sealed record BudgetLedgerReleased: BudgetLedgerReleaseResult
{
    /// <summary>Initializes a released receipt.</summary><param name="reservation">The non-null exact reservation that is now released.</param><exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public BudgetLedgerReleased(BudgetLedgerReservationReference reservation) { ArgumentNullException.ThrowIfNull(reservation); Reservation = reservation; }
    /// <summary>Gets the released reservation locator.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
}
