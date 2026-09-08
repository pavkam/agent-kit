// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no proof or usage quantity permits the started reservation to be released.</summary>
public sealed record BudgetLedgerReconciliationRetainedUnknown: BudgetLedgerReconciliationResult
{
    /// <summary>Initializes an unknown-retained receipt.</summary><param name="reservation">The non-null exact reservation still retained.</param><exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public BudgetLedgerReconciliationRetainedUnknown(BudgetLedgerReservationReference reservation) { ArgumentNullException.ThrowIfNull(reservation); Reservation = reservation; }
    /// <summary>Gets the reservation remaining unresolved.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
}
