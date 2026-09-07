// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The reservation entered started accounting successfully.</summary>
/// <remarks>
/// This outcome permits the budget-controlled operation to continue, but
/// carries no security authority and does not replace authorization at the
/// effecting boundary.
/// </remarks>
public sealed record BudgetStarted: BudgetStartResult
{
    /// <summary>Initializes a successful start-accounting receipt.</summary>
    /// <param name="reservationId">The non-default reservation placed into started accounting.</param>
    /// <param name="wasAlreadyStarted"><see langword="true"/> when the same transition completed earlier.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="reservationId"/> is default.</exception>
    public BudgetStarted(BudgetReservationId reservationId, bool wasAlreadyStarted)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default, nameof(reservationId));
        ReservationId = reservationId;
        WasAlreadyStarted = wasAlreadyStarted;
    }

    /// <summary>Gets the non-default identity of the reservation now accounted as started.</summary>
    public BudgetReservationId ReservationId { get; }

    /// <summary>Gets whether this receipt replays an earlier successful start transition.</summary>
    public bool WasAlreadyStarted { get; }
}
