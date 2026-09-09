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
        : this(reservationId, previousActual, correctedActual, revision, null, [], [])
    {
    }

    /// <summary>Initializes correction evidence including its accounting generation and hold transitions.</summary>
    /// <param name="reservationId">The corrected reservation.</param><param name="previousActual">The replaced value.</param><param name="correctedActual">The truthful replacement.</param><param name="revision">The caller replay revision.</param><param name="accountingRevision">The ledger revision, or null only for compatibility-created values.</param><param name="createdOverrunHolds">New generations created by crossing into overrun.</param><param name="clearedOverrunHolds">Automatic generations cleared by eligible accounting.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity, amount, or revision is invalid.</exception><exception cref="ArgumentException">A hold array is default or contains null.</exception>
    public BudgetCorrectionResult(BudgetReservationId reservationId, decimal previousActual, decimal correctedActual, long revision, BudgetAccountingRevision? accountingRevision, ImmutableArray<BudgetOverrunHold> createdOverrunHolds, ImmutableArray<BudgetOverrunHoldReference> clearedOverrunHolds)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, default, nameof(reservationId));
        ArgumentOutOfRangeException.ThrowIfNegative(previousActual);
        ArgumentOutOfRangeException.ThrowIfNegative(correctedActual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        if (accountingRevision is { } presentAccountingRevision)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(presentAccountingRevision, default, nameof(accountingRevision));
        }
        ArgumentException.ThrowIfDefault(createdOverrunHolds);
        ArgumentException.ThrowIfContainsNull(createdOverrunHolds);
        ArgumentException.ThrowIfDefault(clearedOverrunHolds);
        ArgumentException.ThrowIfContainsNull(clearedOverrunHolds);
        ReservationId = reservationId;
        PreviousActual = previousActual;
        CorrectedActual = correctedActual;
        Revision = revision;
        AccountingRevision = accountingRevision;
        CreatedOverrunHolds = createdOverrunHolds;
        ClearedOverrunHolds = clearedOverrunHolds;
    }

    /// <summary>Gets the original reservation identity.</summary>
    public BudgetReservationId ReservationId { get; }

    /// <summary>Gets the previously recorded actual amount.</summary>
    public decimal PreviousActual { get; }

    /// <summary>Gets the authoritative replacement amount.</summary>
    public decimal CorrectedActual { get; }

    /// <summary>Gets the monotonic correction revision.</summary>
    public long Revision { get; }
    /// <summary>Gets the ledger accounting revision.</summary><value>A positive revision for ledger-produced results; null for compatibility-created values.</value>
    public BudgetAccountingRevision? AccountingRevision { get; }
    /// <summary>Gets generations created by this correction.</summary><value>An initialized lineage-ordered array.</value>
    public ImmutableArray<BudgetOverrunHold> CreatedOverrunHolds { get; }
    /// <summary>Gets automatic generations cleared by this correction.</summary><value>An initialized array of exact old generations.</value>
    public ImmutableArray<BudgetOverrunHoldReference> ClearedOverrunHolds { get; }

    /// <summary>Compares scalar correction and ordered hold-transition contents.</summary><param name="other">The candidate receipt.</param><returns>True when every value and ordered hold array is equal.</returns>
    public bool Equals(BudgetCorrectionResult? other) =>
        other is not null
        && ReservationId == other.ReservationId
        && PreviousActual == other.PreviousActual
        && CorrectedActual == other.CorrectedActual
        && Revision == other.Revision
        && AccountingRevision == other.AccountingRevision
        && CreatedOverrunHolds.SequenceEqual(other.CreatedOverrunHolds)
        && ClearedOverrunHolds.SequenceEqual(other.ClearedOverrunHolds);

    /// <summary>Computes a content hash consistent with ordered equality.</summary><returns>A hash over every scalar and hold transition.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ReservationId); hash.Add(PreviousActual); hash.Add(CorrectedActual); hash.Add(Revision); hash.Add(AccountingRevision);
        foreach (var hold in CreatedOverrunHolds)
        {
            hash.Add(hold);
        }

        foreach (var hold in ClearedOverrunHolds)
        {
            hash.Add(hold);
        }

        return hash.ToHashCode();
    }
}
