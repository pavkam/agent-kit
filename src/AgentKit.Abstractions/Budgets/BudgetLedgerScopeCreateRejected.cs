// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that scope creation was rejected before the scope was persisted.</summary>
public sealed record BudgetLedgerScopeCreateRejected: BudgetLedgerScopeCreateResult
{
    /// <summary>Initializes a rejected scope result.</summary><param name="failure">The non-null typed reason.</param><exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public BudgetLedgerScopeCreateRejected(BudgetScopeCreationFailed failure) { ArgumentNullException.ThrowIfNull(failure); Failure = failure; }
    /// <summary>Gets the typed reason no scope was created.</summary><value>Never null.</value>
    public BudgetScopeCreationFailed Failure { get; }
}
