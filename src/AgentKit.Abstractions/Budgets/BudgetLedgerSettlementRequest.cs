// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests idempotent settlement of one exact reservation with known actual usage.</summary>
public sealed record BudgetLedgerSettlementRequest
{
    /// <summary>Initializes a settlement transition.</summary>
    /// <param name="reservation">The non-null exact reservation locator.</param>
    /// <param name="actual">The nonnegative actual usage.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="actual"/> is negative.</exception>
    public BudgetLedgerSettlementRequest(BudgetLedgerReservationReference reservation, decimal actual)
    { ArgumentNullException.ThrowIfNull(reservation); ArgumentOutOfRangeException.ThrowIfNegative(actual); Reservation = reservation; Actual = actual; }
    /// <summary>Gets the exact reservation to settle.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
    /// <summary>Gets known actual usage.</summary><value>A nonnegative quantity in the reservation's unit.</value>
    public decimal Actual { get; }
}
