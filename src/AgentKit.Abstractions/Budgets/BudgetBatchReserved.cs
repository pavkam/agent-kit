// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Every batch member was atomically reserved in request order.</summary>
public sealed record BudgetBatchReserved: BudgetBatchReservationResult
{
    /// <summary>Initializes a successful batch result.</summary>
    /// <param name="reservations">The non-default, non-empty ordered reservation handles.</param>
    /// <exception cref="ArgumentException"><paramref name="reservations"/> is default, empty, or contains a null handle.</exception>
    public BudgetBatchReserved(ImmutableArray<IBudgetReservation> reservations)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(reservations);
        ArgumentException.ThrowIfContainsNull(reservations);
        Reservations = reservations;
    }

    /// <summary>Gets the non-null owned reservation handles in the request's original order.</summary>
    public ImmutableArray<IBudgetReservation> Reservations { get; }
}
