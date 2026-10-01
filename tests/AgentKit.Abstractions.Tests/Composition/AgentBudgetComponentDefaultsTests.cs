// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentBudgetComponentDefaults behavior and contracts.</summary>
public sealed class AgentBudgetComponentDefaultsTests
{
    [Fact]
    public void ProfileKey_WhenRead_MatchesProfileKeyValue() =>
        AgentBudgetComponentDefaults.ProfileKey.ShouldBe(new BudgetProfileKey(AgentBudgetComponentDefaults.ProfileKeyValue));

    [Fact]
    public void ProfileKeyValue_WhenRead_IsNonblank() =>
        AgentBudgetComponentDefaults.ProfileKeyValue.ShouldNotBeNullOrWhiteSpace();
}
