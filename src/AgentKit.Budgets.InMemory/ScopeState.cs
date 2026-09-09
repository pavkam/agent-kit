// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Retains the immutable admission evidence and mutable reservation index for one in-memory scope.</summary>
internal sealed class ScopeState
{
    /// <summary>Initializes state after every admission computation succeeds.</summary>
    /// <param name="reference">The exact persisted scope locator.</param>
    /// <param name="request">The immutable creation evidence.</param>
    /// <param name="parent">The parent state, when present.</param>
    /// <param name="depth">The positive lineage depth including this scope.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not positive.</exception>
    internal ScopeState(BudgetLedgerScopeReference reference, BudgetLedgerScopeCreateRequest request, ScopeState? parent, int depth)
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
    internal BudgetLedgerScopeReference Reference { get; }
    /// <summary>Gets the immutable original request and captured admission bounds.</summary>
    internal BudgetLedgerScopeCreateRequest Request { get; }
    /// <summary>Gets the structural parent used for hierarchy-wide enforcement.</summary>
    internal ScopeState? Parent { get; }
    /// <summary>Gets the positive lineage depth including this scope.</summary>
    internal int Depth { get; }
    /// <summary>Gets reservations charged to this boundary, including descendant reservations.</summary>
    internal List<ReservationState> Reservations { get; } = [];
    /// <summary>Gets durable overrun generations owned by this exact boundary.</summary>
    internal List<OverrunHoldState> OverrunHolds { get; } = [];
}
