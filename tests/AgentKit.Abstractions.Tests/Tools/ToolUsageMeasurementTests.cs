// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolUsageMeasurement behavior and contracts.</summary>
public sealed class ToolUsageMeasurementTests
{
    [Fact]
    public void ToolUsageMeasurement_Constructor_WhenQualityAndAmountConflict_ThrowsExactException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolUsageMeasurement(new BudgetDimension("requests"), new BudgetUnit("count"), null, ToolUsageMeasurementQuality.Measured));
        exception.ParamName.ShouldBe("amount");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var dimension = new BudgetDimension("requests");
        var unit = new BudgetUnit("count");
        var amount = BudgetQuantity.FromDecimal(1);
        var measurement = new ToolUsageMeasurement(dimension, unit, amount, ToolUsageMeasurementQuality.Measured);
        measurement.Dimension.ShouldBe(dimension);
        measurement.Unit.ShouldBe(unit);
        measurement.Amount.ShouldBe(amount);
        measurement.Quality.ShouldBe(ToolUsageMeasurementQuality.Measured);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolUsageMeasurement(new BudgetDimension("requests"), new BudgetUnit("count"), BudgetQuantity.FromDecimal(1), ToolUsageMeasurementQuality.Measured);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
