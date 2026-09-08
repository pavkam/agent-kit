// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that no batch member was reserved because an enforced limit rejected admission.</summary>
public sealed record BudgetLedgerBatchReserveRejected: BudgetLedgerBatchReserveResult
{
    /// <summary>Initializes a rejected batch receipt.</summary><param name="failure">The non-null limiting boundary.</param><exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public BudgetLedgerBatchReserveRejected(BudgetLimitFailure failure) { ArgumentNullException.ThrowIfNull(failure); Failure = failure; }
    /// <summary>Gets the enforced limit that rejected the whole batch.</summary><value>Never null.</value>
    public BudgetLimitFailure Failure { get; }
}
