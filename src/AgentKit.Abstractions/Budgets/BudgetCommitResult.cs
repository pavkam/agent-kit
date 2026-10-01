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
    /// <summary>Initializes authoritative settlement evidence including its ledger accounting revision and created holds.</summary>
    /// <param name="reservationId">The settled reservation.</param>
    /// <param name="reserved">The originally reserved amount.</param>
    /// <param name="actual">The truthful actual amount committed.</param>
    /// <param name="released">The unused amount released back to the scope.</param>
    /// <param name="overrun">The amount by which <paramref name="actual"/> exceeded <paramref name="reserved"/>, if any.</param>
    /// <param name="accountingRevision">The positive ledger revision that recorded this settlement.</param>
    /// <param name="createdOverrunHolds">The holds created by this accounting transition.</param>
    /// <exception cref="ArgumentOutOfRangeException">An amount is negative or <paramref name="accountingRevision"/> is the default value.</exception>
    /// <exception cref="ArgumentException"><paramref name="createdOverrunHolds"/> is default or contains null.</exception>
    public BudgetCommitResult(
        BudgetReservationId reservationId,
        decimal reserved,
        decimal actual,
        decimal released,
        decimal overrun,
        BudgetAccountingRevision accountingRevision,
        ImmutableArray<BudgetOverrunHold> createdOverrunHolds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reserved);
        ArgumentOutOfRangeException.ThrowIfNegative(actual);
        ArgumentOutOfRangeException.ThrowIfNegative(released);
        ArgumentOutOfRangeException.ThrowIfNegative(overrun);
        ArgumentOutOfRangeException.ThrowIfEqual(accountingRevision, default);
        ArgumentException.ThrowIfDefault(createdOverrunHolds);
        ArgumentException.ThrowIfContainsNull(createdOverrunHolds);

        ReservationId = reservationId;
        Reserved = reserved;
        Actual = actual;
        Released = released;
        Overrun = overrun;
        AccountingRevision = accountingRevision;
        CreatedOverrunHolds = createdOverrunHolds;
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

    /// <summary>Gets the ledger accounting revision.</summary>
    /// <value>The positive revision at which the ledger recorded this settlement.</value>
    public BudgetAccountingRevision AccountingRevision { get; }

    /// <summary>Gets holds created atomically with settlement.</summary><value>An initialized immutable array ordered by charged lineage.</value>
    public ImmutableArray<BudgetOverrunHold> CreatedOverrunHolds { get; }

    /// <summary>Compares scalar accounting and ordered hold contents.</summary><param name="other">The candidate receipt.</param><returns>True when every scalar and ordered hold value is equal.</returns>
    public bool Equals(BudgetCommitResult? other) =>
        other is not null
        && ReservationId == other.ReservationId
        && Reserved == other.Reserved
        && Actual == other.Actual
        && Released == other.Released
        && Overrun == other.Overrun
        && AccountingRevision == other.AccountingRevision
        && CreatedOverrunHolds.SequenceEqual(other.CreatedOverrunHolds);

    /// <summary>Computes a content hash consistent with ordered equality.</summary><returns>A hash over every scalar and hold.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ReservationId); hash.Add(Reserved); hash.Add(Actual); hash.Add(Released); hash.Add(Overrun); hash.Add(AccountingRevision);
        foreach (var hold in CreatedOverrunHolds)
        {
            hash.Add(hold);
        }

        return hash.ToHashCode();
    }
}
