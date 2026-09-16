// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolUsage behavior and contracts.</summary>
public sealed class ToolUsageTests
{
    [Fact]
    public void ToolUsage_Constructor_WhenDimensionAndUnitDuplicate_ThrowsExactException()
    {
        var first = new ToolUsageMeasurement(new BudgetDimension("requests"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1), ToolUsageMeasurementQuality.Measured);
        var second = new ToolUsageMeasurement(first.Dimension, first.Unit, BudgetQuantity.FromDecimal(2), ToolUsageMeasurementQuality.Estimated);
        var exception = Should.Throw<ArgumentException>(() => new ToolUsage([first, second], ExtensionData.Empty));
        exception.ParamName.ShouldBe("measurements");
    }

    [Fact]
    public void ToolUsage_Equality_WhenEquivalentArraysDifferByInstance_IsStructural()
    {
        var measurement = new ToolUsageMeasurement(new BudgetDimension("requests"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1), ToolUsageMeasurementQuality.Measured);
        var first = new ToolUsage([measurement], ExtensionData.Empty);
        var same = new ToolUsage([measurement], ExtensionData.Empty);
        var empty = new ToolUsage([], ExtensionData.Empty);
        first.ShouldBe(same);
        first.GetHashCode().ShouldBe(same.GetHashCode());
        first.ShouldNotBe(empty);
    }

    [Fact]
    public void ToolUsage_Constructor_WhenArrayDefault_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolUsage(default, ExtensionData.Empty));
        exception.ParamName.ShouldBe("measurements");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var measurement = new ToolUsageMeasurement(new BudgetDimension("requests"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1), ToolUsageMeasurementQuality.Measured);
        var original = new ToolUsage([measurement], ExtensionData.Empty);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
