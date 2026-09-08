// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests one bounded page of unresolved started reservations for an exact scope.</summary>
public sealed record BudgetUnresolvedReservationQuery
{
    /// <summary>Initializes a bounded unresolved-reservation query.</summary><param name="scope">The non-null exact scan scope.</param><param name="pageSize">The positive maximum item count. The selected ledger rejects values above its captured finite limit.</param><param name="after">The prior cursor, or null for a new scan.</param><exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception><exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> is not positive.</exception><exception cref="ArgumentException"><paramref name="after"/> belongs to another scope.</exception>
    public BudgetUnresolvedReservationQuery(BudgetLedgerScopeReference scope, int pageSize, BudgetReservationCursor? after)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        if (after is not null)
        {
            ArgumentException.ThrowIfNotEqual(after.Scope, scope, nameof(after));
        }
        Scope = scope;
        PageSize = pageSize;
        After = after;
    }
    /// <summary>Gets the exact scope being scanned.</summary><value>Never null.</value>
    public BudgetLedgerScopeReference Scope { get; }
    /// <summary>Gets the requested maximum number of returned rows.</summary><value>A positive value bounded by the selected ledger's captured capability.</value>
    public int PageSize { get; }
    /// <summary>Gets the cursor from the previous page, if any.</summary><value>Null starts a new watermark-anchored scan.</value>
    public BudgetReservationCursor? After { get; }
}
