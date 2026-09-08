// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed storage outcome for one atomic reservation batch.</summary>
public abstract record BudgetLedgerBatchReserveResult
{
    /// <summary>Prevents external outcome categories.</summary>
    private protected BudgetLedgerBatchReserveResult() { }
}
