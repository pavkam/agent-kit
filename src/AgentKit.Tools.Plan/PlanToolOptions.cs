// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Plan;

/// <summary>Configures model-facing work-plan bounds.</summary>
public sealed class PlanToolOptions
{
    /// <summary>Gets or sets the maximum plan title characters.</summary>
    public int MaximumTitleCharacters { get; set; } = 200;

    /// <summary>Gets or sets the maximum item text characters.</summary>
    public int MaximumItemCharacters { get; set; } = 1_000;

    /// <summary>Gets or sets the maximum stable item-identity characters.</summary>
    public int MaximumItemIdCharacters { get; set; } = 100;

    /// <summary>Gets or sets the maximum items in one plan.</summary>
    public int MaximumItems { get; set; } = 50;
}
