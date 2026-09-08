// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed receipt for an attempt to release an unstarted reservation.</summary>
public abstract record BudgetLedgerReleaseResult
{
    /// <summary>Prevents external outcome categories.</summary>
    private protected BudgetLedgerReleaseResult() { }
}
