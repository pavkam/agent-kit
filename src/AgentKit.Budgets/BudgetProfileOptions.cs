// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Mutable configuration for one named budget profile registered through DI.</summary>
public sealed class BudgetProfileOptions
{
    /// <summary>Gets or sets the positive profile revision published with this registration.</summary>
    public BudgetProfileVersion Version { get; set; } = new(1);

    /// <summary>Gets the limits configured on this profile.</summary>
    public List<BudgetLimit> Limits { get; } = [];

    /// <summary>Gets the ordered policy keys evaluated for scopes created from this profile.</summary>
    public List<BudgetPolicyKey> Policies { get; } = [];
}
