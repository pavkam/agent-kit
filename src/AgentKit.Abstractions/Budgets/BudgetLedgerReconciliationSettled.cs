// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that measured or estimated reconciliation evidence settled the reservation.</summary>
public sealed record BudgetLedgerReconciliationSettled: BudgetLedgerReconciliationResult
{
    /// <summary>Initializes a reconciliation settlement receipt.</summary><param name="commit">The non-null resulting commitment accounting.</param><exception cref="ArgumentNullException"><paramref name="commit"/> is null.</exception>
    public BudgetLedgerReconciliationSettled(BudgetCommitResult commit) { ArgumentNullException.ThrowIfNull(commit); Commit = commit; }
    /// <summary>Gets the resulting settlement accounting.</summary><value>Never null.</value>
    public BudgetCommitResult Commit { get; }
}
