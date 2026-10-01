// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Allows a delegation, optionally narrowing its scope or budget.</summary>
/// <remarks>A policy can only narrow. The pipeline verifies that each narrowed value is contained in the value it replaces and turns a widening into a denial, so a policy can never broaden the parent's authority.</remarks>
public sealed record DelegationPolicyAllowed: DelegationPolicyDecision
{
    /// <summary>Initializes an allow decision.</summary>
    /// <param name="scope">The narrowed scope, or <see langword="null"/> to keep the current one.</param>
    /// <param name="budget">The narrowed budget, or <see langword="null"/> to keep the current one.</param>
    public DelegationPolicyAllowed(DelegationScope? scope = null, GoalBudget? budget = null)
    {
        Scope = scope;
        Budget = budget;
    }

    /// <summary>Gets the narrowed scope, or <see langword="null"/> when unchanged.</summary>
    public DelegationScope? Scope { get; }

    /// <summary>Gets the narrowed budget, or <see langword="null"/> when unchanged.</summary>
    public GoalBudget? Budget { get; }
}
