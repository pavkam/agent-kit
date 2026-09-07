// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Distinguishes an enforced ceiling from an observational threshold.</summary>
public enum BudgetLimitKind
{
    /// <summary>
    /// Crossing this limit is recorded but does not itself reject a
    /// reservation. Soft limits exist for visibility, not enforcement.
    /// </summary>
    Soft,

    /// <summary>
    /// Crossing this limit rejects the reservation that would exceed it.
    /// </summary>
    Hard
}
