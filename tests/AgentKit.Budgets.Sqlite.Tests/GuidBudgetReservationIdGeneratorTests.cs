// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies GuidBudgetReservationIdGenerator behavior and contracts.</summary>
public sealed class GuidBudgetReservationIdGeneratorTests
{
    /// <summary>Verifies every created identity is a nondefault, unique GUID-backed value.</summary>
    [Fact]
    public void Create_WhenCalledRepeatedly_ReturnsUniqueNondefaultIdentities()
    {
        var generator = new GuidBudgetReservationIdGenerator();
        var first = generator.Create();
        var second = generator.Create();
        first.ShouldNotBe(default);
        second.ShouldNotBe(default);
        first.ShouldNotBe(second);
    }
}
