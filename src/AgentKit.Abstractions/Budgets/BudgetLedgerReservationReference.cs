// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Exactly locates one persisted reservation under its structural scope.</summary>
public sealed record BudgetLedgerReservationReference
{
    /// <summary>Initializes an exact reservation locator.</summary><param name="scope">The non-null structural scope locator.</param><param name="id">The nondefault persisted reservation identity.</param><exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception><exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception>
    public BudgetLedgerReservationReference(BudgetLedgerScopeReference scope, BudgetReservationId id)
    { ArgumentNullException.ThrowIfNull(scope); ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id)); Scope = scope; Id = id; }
    /// <summary>Gets the scope that owns this reservation.</summary><value>An exact structural locator.</value>
    public BudgetLedgerScopeReference Scope { get; }
    /// <summary>Gets the persisted reservation identity.</summary><value>A nondefault opaque identifier.</value>
    public BudgetReservationId Id { get; }
}
