// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Controls when one scope boundary may clear truthful overrun admission holds.</summary>
public enum BudgetOverrunHoldPolicy
{
    /// <summary>Clears automatically after corrected accounting has no row overrun and remains within finite hard ceilings.</summary>
    ClearWhenReconciled,
    /// <summary>Requires an audited operator resolution after accounting becomes eligible for clearance.</summary>
    RequireAuthorizedResolution,
}
