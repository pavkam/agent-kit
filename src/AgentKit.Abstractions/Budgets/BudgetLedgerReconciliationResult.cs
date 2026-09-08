// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome from reconciling unresolved started reservation capacity.</summary>
public abstract record BudgetLedgerReconciliationResult
{
    /// <summary>Prevents external outcome categories.</summary>
    private protected BudgetLedgerReconciliationResult() { }
}
