// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the default GUID-backed scope-identity generator used when a host supplies no replacement.</summary>
public sealed class GuidBudgetScopeIdGeneratorTests
{
    /// <summary>Verifies every created identity is a nondefault, unique GUID-backed value.</summary>
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsUniqueNondefaultIdentities()
    {
        var generator = new GuidBudgetScopeIdGenerator();

        var first = generator.Create();
        var second = generator.Create();

        first.ShouldNotBe(default);
        second.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
