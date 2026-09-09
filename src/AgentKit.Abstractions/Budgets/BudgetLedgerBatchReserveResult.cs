// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed storage outcome: reserved receipts, numeric limit rejection, or truthful active-hold refusal for one atomic batch.</summary>
public abstract record BudgetLedgerBatchReserveResult
{
    /// <summary>Prevents external outcome categories.</summary>
    private protected BudgetLedgerBatchReserveResult() { }
}
