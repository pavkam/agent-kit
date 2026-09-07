// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Records one authoritative replacement of committed reservation accounting.</summary>
public sealed record BudgetCorrectionResult
{
    /// <summary>Initializes an applied correction result.</summary>
    /// <param name="reservationId">The original reservation whose accounting was replaced.</param>
    /// <param name="previousActual">The actual amount recorded before this correction.</param>
    /// <param name="correctedActual">The authoritative replacement amount.</param>
    /// <param name="revision">The positive monotonic correction revision.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="reservationId"/> is default, an amount is negative, or
    /// <paramref name="revision"/> is not positive.
    /// </exception>
    public BudgetCorrectionResult(
        BudgetReservationId reservationId,
        decimal previousActual,
        decimal correctedActual,
        long revision)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default, nameof(reservationId));
        ArgumentOutOfRangeException.ThrowIfNegative(previousActual);
        ArgumentOutOfRangeException.ThrowIfNegative(correctedActual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        ReservationId = reservationId;
        PreviousActual = previousActual;
        CorrectedActual = correctedActual;
        Revision = revision;
    }

    /// <summary>Gets the original reservation identity.</summary>
    public BudgetReservationId ReservationId { get; }

    /// <summary>Gets the previously recorded actual amount.</summary>
    public decimal PreviousActual { get; }

    /// <summary>Gets the authoritative replacement amount.</summary>
    public decimal CorrectedActual { get; }

    /// <summary>Gets the monotonic correction revision.</summary>
    public long Revision { get; }
}
