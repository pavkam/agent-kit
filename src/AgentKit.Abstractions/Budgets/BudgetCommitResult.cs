// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The outcome of committing actual usage against one
/// <see cref="IBudgetReservation"/>: how much of the original reservation
/// was released back, and how much the actual usage overran it, if any.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Exactly
/// one of <see cref="Released"/> and <see cref="Overrun"/> is normally
/// non-zero: actual usage at or under the reservation releases the unused
/// remainder, while actual usage above the reservation records the excess
/// as an overrun rather than silently making a hard limit negative.
/// </remarks>
public sealed record BudgetCommitResult
{
    /// <summary>Initializes a new instance of the <see cref="BudgetCommitResult"/> record.</summary>
    /// <param name="reservationId">The reservation this result settles.</param>
    /// <param name="reserved">The originally reserved amount.</param>
    /// <param name="actual">The actual amount committed.</param>
    /// <param name="released">The unused amount released back to the scope.</param>
    /// <param name="overrun">The amount by which <paramref name="actual"/> exceeded <paramref name="reserved"/>, if any.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="reserved"/>, <paramref name="actual"/>, <paramref name="released"/>,
    /// or <paramref name="overrun"/> is negative.
    /// </exception>
    public BudgetCommitResult(
        BudgetReservationId reservationId,
        decimal reserved,
        decimal actual,
        decimal released,
        decimal overrun)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reserved);
        ArgumentOutOfRangeException.ThrowIfNegative(actual);
        ArgumentOutOfRangeException.ThrowIfNegative(released);
        ArgumentOutOfRangeException.ThrowIfNegative(overrun);

        ReservationId = reservationId;
        Reserved = reserved;
        Actual = actual;
        Released = released;
        Overrun = overrun;
    }

    /// <summary>Gets the reservation this result settles.</summary>
    public BudgetReservationId ReservationId { get; init; }

    /// <summary>Gets the originally reserved amount.</summary>
    public decimal Reserved { get; init; }

    /// <summary>Gets the actual amount committed.</summary>
    public decimal Actual { get; init; }

    /// <summary>Gets the unused amount released back to the scope.</summary>
    public decimal Released { get; init; }

    /// <summary>Gets the amount by which <see cref="Actual"/> exceeded <see cref="Reserved"/>, if any.</summary>
    public decimal Overrun { get; init; }
}
