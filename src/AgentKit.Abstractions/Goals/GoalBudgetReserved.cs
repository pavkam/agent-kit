// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a reserved child budget.</summary>
public sealed record GoalBudgetReserved: GoalBudgetReserveResult
{
    /// <summary>Initializes a reserved result.</summary>
    /// <param name="reservation">The reservation, carrying the scope that holds it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reservation"/> is null.</exception>
    public GoalBudgetReserved(GoalBudgetReservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        Reservation = reservation;
    }

    /// <summary>Gets the reservation.</summary>
    public GoalBudgetReservation Reservation { get; }
}
