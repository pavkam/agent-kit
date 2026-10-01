// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The abstract outcome of reserving a child's budget.</summary>
public abstract record GoalBudgetReserveResult
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected GoalBudgetReserveResult()
    {
    }
}
