// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Retains one decoded, validated scope row and its structural database projections.</summary>
internal sealed record SqliteScopeRow
{
    /// <summary>Creates a scope row after validating decoded reference and request evidence.</summary>
    /// <param name="reference">The exact persisted scope reference.</param>
    /// <param name="request">The immutable creation evidence.</param>
    /// <param name="parentId">The projected parent identity, when present.</param>
    /// <param name="depth">The positive projected lineage depth.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is not positive.</exception>
    internal SqliteScopeRow(BudgetLedgerScopeReference reference, BudgetLedgerScopeCreateRequest request, BudgetScopeId? parentId, int depth)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        Reference = reference;
        Request = request;
        ParentId = parentId;
        Depth = depth;
    }

    /// <summary>Gets the exact persisted reference.</summary><value>The validated identity and owner address.</value>
    internal BudgetLedgerScopeReference Reference { get; }
    /// <summary>Gets immutable scope creation evidence.</summary><value>The exact decoded request.</value>
    internal BudgetLedgerScopeCreateRequest Request { get; }
    /// <summary>Gets the projected parent.</summary><value>The parent identity, or null for a root.</value>
    internal BudgetScopeId? ParentId { get; }
    /// <summary>Gets the projected depth.</summary><value>A positive lineage depth.</value>
    internal int Depth { get; }
}
