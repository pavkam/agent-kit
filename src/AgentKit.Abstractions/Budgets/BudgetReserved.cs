// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Capacity was atomically reserved and the caller owns the returned handle.</summary>
public sealed record BudgetReserved: BudgetReservationResult
{
    /// <summary>Initializes a new instance of the <see cref="BudgetReserved"/> record.</summary>
    /// <param name="reservation">The owned reservation handle.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public BudgetReserved(IBudgetReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        Reservation = reservation;
    }

    /// <summary>Gets the owned reservation handle.</summary>
    public IBudgetReservation Reservation { get; init; }
}
