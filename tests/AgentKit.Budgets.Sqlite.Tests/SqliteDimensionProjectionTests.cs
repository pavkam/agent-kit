// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite.Tests;

/// <summary>Verifies SqliteDimensionProjection behavior and contracts.</summary>
public sealed class SqliteDimensionProjectionTests
{
    /// <summary>Verifies a valid construction exposes exactly its captured evidence.</summary>
    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesExactCapturedValues()
    {
        var dimension = new BudgetDimension("tokens");
        var unit = new BudgetUnit("count");
        var reserved = BudgetQuantity.FromDecimal(3);
        var committed = BudgetQuantity.FromDecimal(1);
        var projection = new SqliteDimensionProjection(dimension, unit, BudgetAggregationKind.Sum, reserved, committed, 2);
        projection.Dimension.ShouldBe(dimension);
        projection.Unit.ShouldBe(unit);
        projection.Aggregation.ShouldBe(BudgetAggregationKind.Sum);
        projection.Reserved.ShouldBe(reserved);
        projection.Committed.ShouldBe(committed);
        projection.OpenCount.ShouldBe(2);
    }

    /// <summary>Verifies a negative open count is rejected.</summary>
    [Fact]
    public void Constructor_WhenOpenCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new SqliteDimensionProjection(
            new BudgetDimension("tokens"), new BudgetUnit("count"), BudgetAggregationKind.Sum, default, default, -1)).ParamName.ShouldBe("openCount");
    }

    /// <summary>Verifies record equality and with-expression cloning preserve every captured field.</summary>
    [Fact]
    public void WithExpression_WhenNoFieldChanges_ClonesEveryField()
    {
        var original = new SqliteDimensionProjection(new BudgetDimension("tokens"), new BudgetUnit("count"), BudgetAggregationKind.Sum, default, default, 0);
        var copy = original with { };
        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
    }
}
