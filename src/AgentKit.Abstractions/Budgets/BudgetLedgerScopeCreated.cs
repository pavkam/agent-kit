// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports a newly created scope or an exact idempotent replay.</summary>
public sealed record BudgetLedgerScopeCreated: BudgetLedgerScopeCreateResult
{
    /// <summary>Initializes a successful scope receipt.</summary><param name="scope">The non-null persisted scope reference.</param><exception cref="ArgumentNullException"><paramref name="scope"/> is null.</exception>
    public BudgetLedgerScopeCreated(BudgetLedgerScopeReference scope) { ArgumentNullException.ThrowIfNull(scope); Scope = scope; }
    /// <summary>Gets the persisted scope locator.</summary><value>Never null.</value>
    public BudgetLedgerScopeReference Scope { get; }
}
