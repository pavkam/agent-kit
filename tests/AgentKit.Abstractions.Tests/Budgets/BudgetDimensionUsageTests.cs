// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

using AgentKit;

/// <summary>Verifies BudgetDimensionUsage behavior and contracts.</summary>
public sealed class BudgetDimensionUsageTests
{
    [Theory]
    [InlineData(-1, 0, "reserved")]
    [InlineData(0, -1, "committed")]
    public void BudgetDimensionUsage_WhenCompatibilityQuantityIsNegative_ThrowsBeforeConstruction(decimal reserved, decimal committed, string parameterName)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetDimensionUsage(new BudgetDimension("tests.requests"), new BudgetUnit("requests"), reserved, committed, null));
        exception.ParamName.ShouldBe(parameterName);
    }
}
