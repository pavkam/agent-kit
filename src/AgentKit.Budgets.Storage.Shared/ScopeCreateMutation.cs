// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Storage;

/// <summary>Describes one fully validated scope insertion for an adapter-owned atomic commit.</summary>
internal sealed record ScopeCreateMutation
{
    /// <summary>Creates a staged mutation after admission, identity, and revision preflight.</summary>
    /// <param name="reference">The exact non-null new scope reference.</param>
    /// <param name="request">The exact non-null creation evidence.</param>
    /// <param name="parentScopeId">The parent identity, or null for a root.</param>
    /// <param name="depth">The positive resulting lineage depth.</param>
    /// <param name="revision">The positive revision assigned by the atomic commit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> or <paramref name="revision"/> is not positive.</exception>
    internal ScopeCreateMutation(BudgetLedgerScopeReference reference, BudgetLedgerScopeCreateRequest request, BudgetScopeId? parentScopeId, int depth, long revision)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        Reference = reference;
        Request = request;
        ParentScopeId = parentScopeId;
        Depth = depth;
        Revision = revision;
    }

    /// <summary>Gets the created scope reference.</summary><value>The exact generated identity and requested address.</value>
    internal BudgetLedgerScopeReference Reference { get; }
    /// <summary>Gets immutable creation evidence.</summary><value>The admitted request persisted for replay.</value>
    internal BudgetLedgerScopeCreateRequest Request { get; }
    /// <summary>Gets the parent projection.</summary><value>The parent identity, or null for a root.</value>
    internal BudgetScopeId? ParentScopeId { get; }
    /// <summary>Gets the resulting depth.</summary><value>A positive lineage depth.</value>
    internal int Depth { get; }
    /// <summary>Gets the assigned ledger revision.</summary><value>A positive atomic commit revision.</value>
    internal long Revision { get; }
}
