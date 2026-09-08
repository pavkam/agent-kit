// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one revisioned replacement of settled reservation accounting.</summary>
public sealed record BudgetLedgerCorrectionRequest
{
    /// <summary>Initializes a correction transition.</summary>
    /// <param name="reservation">The non-null exact reservation locator.</param>
    /// <param name="correctedActual">The nonnegative authoritative replacement usage.</param>
    /// <param name="revision">The positive monotonic correction revision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="correctedActual"/> is negative or <paramref name="revision"/> is not positive.</exception>
    public BudgetLedgerCorrectionRequest(BudgetLedgerReservationReference reservation, decimal correctedActual, long revision)
    { ArgumentNullException.ThrowIfNull(reservation); ArgumentOutOfRangeException.ThrowIfNegative(correctedActual); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision); Reservation = reservation; CorrectedActual = correctedActual; Revision = revision; }
    /// <summary>Gets the exact reservation whose accounting changes.</summary><value>Never null.</value>
    public BudgetLedgerReservationReference Reservation { get; }
    /// <summary>Gets replacement actual usage.</summary><value>A nonnegative quantity in the reservation's unit.</value>
    public decimal CorrectedActual { get; }
    /// <summary>Gets the positive monotonic correction revision.</summary><value>A revision that prevents duplicate adjustment.</value>
    public long Revision { get; }
}
