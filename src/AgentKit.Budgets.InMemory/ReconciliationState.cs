// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Binds one reconciliation key to exact evidence and its immutable outcome.</summary>
internal sealed record ReconciliationState
{
    /// <summary>Creates replay state only after the reconciliation transition succeeds.</summary>
    /// <param name="evidence">The closed caller evidence used for conflict detection.</param>
    /// <param name="result">The persisted reconciliation outcome.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    internal ReconciliationState(BudgetReconciliationEvidence evidence, BudgetLedgerReconciliationResult result)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(result);
        Evidence = evidence;
        Result = result;
    }

    /// <summary>Gets the evidence used for exact replay and conflicting-retry detection.</summary>
    internal BudgetReconciliationEvidence Evidence { get; }

    /// <summary>Gets the immutable terminal outcome returned by every exact replay.</summary>
    internal BudgetLedgerReconciliationResult Result { get; }
}
