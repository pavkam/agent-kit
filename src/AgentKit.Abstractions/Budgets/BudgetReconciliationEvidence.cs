// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed evidence supplied to reconcile possibly consumed started reservation capacity.</summary>
/// <remarks>The variants describe proof, measured usage, conservative estimation, or remaining uncertainty. They never infer non-consumption from timeout or process loss.</remarks>
public abstract record BudgetReconciliationEvidence
{
    /// <summary>Prevents external evidence categories.</summary>
    private protected BudgetReconciliationEvidence() { }
}
