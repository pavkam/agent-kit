// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetNoUsageProven behavior and contracts.</summary>
public sealed class BudgetNoUsageProvenTests
{
    [Fact]
    public void Equals_WhenComparedThroughBaseType_UsesValueEquality()
    {
        BudgetReconciliationEvidence first = new BudgetNoUsageProven();
        BudgetReconciliationEvidence second = new BudgetNoUsageProven();
        first.Equals(second).ShouldBeTrue();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetNoUsageProven();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
