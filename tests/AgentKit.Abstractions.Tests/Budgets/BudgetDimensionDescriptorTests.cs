// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetDimensionDescriptor behavior and contracts.</summary>
public sealed class BudgetDimensionDescriptorTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var dimension = new BudgetDimension("tests.requests");
        ImmutableArray<BudgetUnit> units = [new("requests")];
        var descriptor = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, units);
        descriptor.Dimension.ShouldBe(dimension);
        descriptor.Aggregation.ShouldBe(BudgetAggregationKind.Sum);
        descriptor.AllowedUnits.ShouldBe(units);
    }

    [Fact]
    public void Constructor_WhenAggregationIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetDimensionDescriptor(new BudgetDimension("tests.requests"), (BudgetAggregationKind) 999, [new BudgetUnit("requests")]));
        exception.ParamName.ShouldBe("aggregation");
    }

    [Fact]
    public void Constructor_WhenAllowedUnitsIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetDimensionDescriptor(new BudgetDimension("tests.requests"), BudgetAggregationKind.Sum, default));
        exception.ParamName.ShouldBe("allowedUnits");
    }

    [Fact]
    public void Constructor_WhenAllowedUnitsIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new BudgetDimensionDescriptor(new BudgetDimension("tests.requests"), BudgetAggregationKind.Sum, []));
        exception.ParamName.ShouldBe("allowedUnits");
    }

    [Fact]
    public void Equals_WhenAllowedUnitsMatch_InstancesAreEqual()
    {
        var dimension = new BudgetDimension("tests.requests");
        ImmutableArray<BudgetUnit> units = [new("requests")];
        var first = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, units);
        var second = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, units);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenAllowedUnitsDiffer_IsNotEqual()
    {
        var dimension = new BudgetDimension("tests.requests");
        var first = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("requests")]);
        var second = new BudgetDimensionDescriptor(dimension, BudgetAggregationKind.Sum, [new BudgetUnit("tokens")]);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new BudgetDimensionDescriptor(new BudgetDimension("tests.requests"), BudgetAggregationKind.Sum, [new BudgetUnit("requests")]);
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
