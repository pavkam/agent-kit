// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports persisted receipts for an accepted all-or-none reservation batch.</summary>
public sealed record BudgetLedgerBatchReserved: BudgetLedgerBatchReserveResult
{
    /// <summary>Initializes an accepted batch receipt.</summary>
    /// <param name="receipts">The initialized nonempty receipts in original request order.</param>
    /// <exception cref="ArgumentException"><paramref name="receipts"/> is default, empty, null-containing, mixed-scope, or repeats a reservation identity or original item key.</exception>
    public BudgetLedgerBatchReserved(ImmutableArray<BudgetLedgerReservationReceipt> receipts)
    {
        ArgumentException.ThrowIfInvalidBudgetLedgerBatchReceipts(receipts, nameof(receipts));
        Receipts = receipts;
    }
    /// <summary>Gets receipts in the original ordered request sequence.</summary><value>A nonempty immutable collection.</value>
    public ImmutableArray<BudgetLedgerReservationReceipt> Receipts { get; }
    /// <inheritdoc/>
    public bool Equals(BudgetLedgerBatchReserved? other) => other is not null && Receipts.SequenceEqual(other.Receipts);
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var receipt in Receipts)
        {
            hash.Add(receipt);
        }
        return hash.ToHashCode();
    }
}
