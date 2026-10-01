// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Provides the canonical default <see cref="BudgetProfileKey"/> an agent definition selects through <see cref="AgentComponentSelection.BudgetProfile"/>.</summary>
/// <remarks>
/// The key names a profile a host registers explicitly through the budgets package; a composition that wants no
/// effective limit registers the profile with no limits rather than omitting the selection. This type is declarative
/// key metadata only: it registers no profile and carries no limit.
/// </remarks>
public static class AgentBudgetComponentDefaults
{
    /// <summary>The string value of <see cref="ProfileKey"/>, exposed as a compile-time constant.</summary>
    public const string ProfileKeyValue = "agentkit-default-budget-profile";

    /// <summary>Gets the canonical default budget profile key.</summary>
    /// <value>A stable, nonblank key shared by every first-party package that registers or selects the default profile.</value>
    public static BudgetProfileKey ProfileKey { get; } = new(ProfileKeyValue);
}
