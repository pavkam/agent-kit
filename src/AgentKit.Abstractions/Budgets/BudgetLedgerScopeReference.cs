// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Exactly locates one persisted budget scope without granting authority to use it.</summary>
public sealed record BudgetLedgerScopeReference
{
    /// <summary>Initializes an exact scope locator.</summary><param name="id">The nondefault persisted scope identity.</param><param name="address">The structural address which must match the persisted scope.</param><exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is default.</exception><exception cref="ArgumentNullException"><paramref name="address"/> is null.</exception>
    public BudgetLedgerScopeReference(BudgetScopeId id, BudgetScopeAddress address)
    { ArgumentOutOfRangeException.ThrowIfEqual(id, default, nameof(id)); ArgumentNullException.ThrowIfNull(address); Id = id; Address = address; }
    /// <summary>Gets the persisted scope identity.</summary><value>A nondefault opaque identifier.</value>
    public BudgetScopeId Id { get; }
    /// <summary>Gets the exact tenant and hierarchy address.</summary><value>Structural evidence checked by the ledger.</value>
    public BudgetScopeAddress Address { get; }
}
