// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetActualEstimated behavior and contracts.</summary>
public sealed class BudgetActualEstimatedTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BudgetActualEstimated_WhenActualIsNegativeOrZero_EnforcesTheDocumentedBoundary(int actual)
    {
        if (actual == 0)
        {
            new BudgetActualEstimated(actual).Actual.ShouldBe(0m);
            return;
        }

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetActualEstimated(actual));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetActualEstimated(1m);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
