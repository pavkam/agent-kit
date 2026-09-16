// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetStillUnknown behavior and contracts.</summary>
public sealed class BudgetStillUnknownTests
{
    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new BudgetStillUnknown();
        var second = new BudgetStillUnknown();
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetStillUnknown();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
