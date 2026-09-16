// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetActualMeasured behavior and contracts.</summary>
public sealed class BudgetActualMeasuredTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void BudgetActualMeasured_WhenActualIsNegative_ThrowsExactParameterName(int actual)
    {
        if (actual == 0)
        {
            _ = Should.NotThrow(() => new BudgetActualMeasured(actual));
            return;
        }

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetActualMeasured(actual));
        exception.ParamName.ShouldBe("actual");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetActualMeasured(1m);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Actual_WhenRead_ReturnsConstructedValue() => new BudgetActualMeasured(2m).Actual.ShouldBe(2m);
}
