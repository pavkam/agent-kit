// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Retains the immutable admission evidence and mutable reservation index for one replayed scope.</summary>
/// <remarks>
/// This is the in-memory projection of a persisted <see cref="JsonBudgetLedgerRecordKind.ScopeCreated"/> record, not a
/// separate source of truth. The journal remains authoritative; every field here is reconstructed by replaying it.
/// Instances are mutated only while the owning ledger's gate is held.
/// </remarks>
internal sealed class ScopeState
{
    /// <summary>Initializes state after every admission computation succeeds.</summary>
    /// <param name="reference">The exact persisted scope locator.</param>
    /// <param name="request">The immutable creation evidence.</param>
    /// <param name="parent">The parent state, or null for a root scope.</param>
    /// <param name="depth">The positive lineage depth including this scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not positive.</exception>
    internal ScopeState(
        BudgetLedgerScopeReference reference, BudgetLedgerScopeCreateRequest request, ScopeState? parent, int depth)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        Reference = reference;
        Request = request;
        Parent = parent;
        Depth = depth;
    }

    /// <summary>Gets the exact scope locator checked on every operation.</summary>
    /// <value>The identity and address a caller reference must match exactly.</value>
    internal BudgetLedgerScopeReference Reference { get; }

    /// <summary>Gets the immutable original request and captured admission bounds.</summary>
    /// <value>The evidence an idempotent scope replay is compared against.</value>
    internal BudgetLedgerScopeCreateRequest Request { get; }

    /// <summary>Gets the structural parent used for hierarchy-wide enforcement.</summary>
    /// <value>The immediate parent state, or null when this scope is a root.</value>
    internal ScopeState? Parent { get; }

    /// <summary>Gets the positive lineage depth including this scope.</summary>
    /// <value>A count compared against the captured maximum depth when admitting a child.</value>
    internal int Depth { get; }

    /// <summary>Gets reservations charged to this boundary, including reservations owned by descendant scopes.</summary>
    /// <value>A mutable list appended under the ledger gate in reservation order.</value>
    internal List<ReservationState> Reservations { get; } = [];

    /// <summary>Gets durable overrun generations owned by this exact boundary.</summary>
    /// <value>A mutable list appended under the ledger gate in creation order.</value>
    internal List<OverrunHoldState> OverrunHolds { get; } = [];
}
