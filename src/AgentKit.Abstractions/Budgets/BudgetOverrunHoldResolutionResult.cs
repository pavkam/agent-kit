// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Closed outcome for audited resolution of one exact overrun-hold generation.</summary>
public abstract record BudgetOverrunHoldResolutionResult
{
    /// <summary>Prevents external outcome categories.</summary>
    private protected BudgetOverrunHoldResolutionResult() { }
}
