// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks the goal budget manager to record what a reservation's holder consumed and release the remainder.</summary>
public sealed record GoalBudgetSettleRequest
{
    /// <summary>Initializes a validated settle request.</summary>
    /// <param name="reservation">The reservation being settled.</param>
    /// <param name="usage">The consumed usage; <see cref="GoalBudgetUsage.None"/> releases the reservation untouched.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public GoalBudgetSettleRequest(GoalBudgetReservation reservation, GoalBudgetUsage usage)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(usage);
        Reservation = reservation;
        Usage = usage;
    }

    /// <summary>Gets the reservation being settled.</summary>
    public GoalBudgetReservation Reservation { get; }

    /// <summary>Gets the consumed usage.</summary>
    public GoalBudgetUsage Usage { get; }
}
