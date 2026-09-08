// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Anchors an unresolved-reservation scan to one scope, ordering position, and ledger snapshot watermark.</summary>
/// <remarks>Adapters order reservation identifiers by ordinal comparison of their canonical lowercase <c>D</c>-format text and persist that text, giving SQLite and in-memory ledgers identical continuation order.</remarks>
public sealed record BudgetReservationCursor
{
    /// <summary>Initializes a continuation cursor.</summary>
    /// <param name="scope">The non-null exact scan scope.</param>
    /// <param name="watermark">The nondefault ledger revision captured for this scan.</param>
    /// <param name="afterReservationId">The nondefault reservation identifier immediately before the next page.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="watermark"/> or <paramref name="afterReservationId"/> is default.</exception>
    public BudgetReservationCursor(BudgetLedgerScopeReference scope, BudgetLedgerWatermark watermark, BudgetReservationId afterReservationId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfEqual(watermark, default, nameof(watermark));
        ArgumentOutOfRangeException.ThrowIfEqual(afterReservationId, default, nameof(afterReservationId));
        Scope = scope;
        Watermark = watermark;
        AfterReservationId = afterReservationId;
    }
    /// <summary>Gets the exact scope to which the cursor is bound.</summary><value>Never null.</value>
    public BudgetLedgerScopeReference Scope { get; }
    /// <summary>Gets the immutable ledger revision bounding this scan.</summary><value>A positive snapshot watermark.</value>
    public BudgetLedgerWatermark Watermark { get; }
    /// <summary>Gets the last canonical reservation identity observed.</summary><value>A nondefault identity used as the exclusive continuation position.</value>
    public BudgetReservationId AfterReservationId { get; }
}
