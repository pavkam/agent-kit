// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Returns one bounded page of unresolved started reservations and its optional continuation.</summary>
public sealed record BudgetUnresolvedReservationPage
{
    /// <summary>Initializes a bounded scan page.</summary>
    /// <param name="scope">The non-null exact scope queried.</param>
    /// <param name="watermark">The nondefault scan revision assigned by the ledger.</param>
    /// <param name="items">The initialized non-null unresolved rows in strict canonical identifier order.</param>
    /// <param name="next">The continuation cursor, or null when no later row was observed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="watermark"/> is default.</exception>
    /// <exception cref="ArgumentException"><paramref name="items"/> or <paramref name="next"/> violates scope, ordering, or continuation invariants.</exception>
    public BudgetUnresolvedReservationPage(BudgetLedgerScopeReference scope, BudgetLedgerWatermark watermark, ImmutableArray<BudgetUnresolvedReservation> items, BudgetReservationCursor? next)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfEqual(watermark, default, nameof(watermark));
        ArgumentException.ThrowIfDefault(items, nameof(items));
        ArgumentException.ThrowIfContainsNull(items, nameof(items));
        ArgumentException.ThrowIfInvalidBudgetUnresolvedReservationPage(scope, watermark, items, next, nameof(items));
        Scope = scope;
        Watermark = watermark;
        Items = items;
        Next = next;
    }
    /// <summary>Gets the exact scope that was scanned.</summary><value>Never null.</value>
    public BudgetLedgerScopeReference Scope { get; }
    /// <summary>Gets the ledger revision that bounds this page and any continuation.</summary><value>A nondefault scan watermark.</value>
    public BudgetLedgerWatermark Watermark { get; }
    /// <summary>Gets rows in canonical reservation identity order.</summary><value>An initialized immutable collection whose query page-size bound the ledger enforces when producing this page.</value>
    public ImmutableArray<BudgetUnresolvedReservation> Items { get; }
    /// <summary>Gets the cursor for a later bounded page.</summary><value>Null when no later eligible row was observed.</value>
    public BudgetReservationCursor? Next { get; }
    /// <inheritdoc/>
    public bool Equals(BudgetUnresolvedReservationPage? other) => other is not null && Scope == other.Scope && Watermark == other.Watermark && Next == other.Next && Items.SequenceEqual(other.Items);
    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Scope);
        hash.Add(Watermark);
        foreach (var item in Items)
        {
            hash.Add(item);
        }
        hash.Add(Next);
        return hash.ToHashCode();
    }
}
