// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;



/// <summary>Verifies BudgetLimit behavior and contracts.</summary>
public sealed class BudgetLimitTests
{
    [Fact]
    public void BudgetLimit_WhenZeroCeilingIsSupplied_PreservesValidBoundary()
    {
        var limit = new BudgetLimit(new BudgetDimension("tests.requests"), 0m, new BudgetUnit("requests"), BudgetLimitKind.Hard);
        limit.Value.ShouldBe(0m);
    }

    [Fact]
    public void BudgetLimit_WhenConstructorDimensionIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetLimit(default, 1m, new BudgetUnit("requests"), BudgetLimitKind.Hard));
        exception.ParamName.ShouldBe("dimension");
    }

    [Fact]
    public void BudgetLimit_WhenConstructorUnitIsDefault_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new BudgetLimit(new BudgetDimension("tests.requests"), 1m, default, BudgetLimitKind.Hard));
        exception.ParamName.ShouldBe("unit");
    }

    [Fact]
    public void BudgetLimit_WhenConstructorValueIsNegative_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLimit(new BudgetDimension("tests.requests"), -1m, new BudgetUnit("requests"), BudgetLimitKind.Hard));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void BudgetLimit_WhenConstructorKindIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BudgetLimit(new BudgetDimension("tests.requests"), 1m, new BudgetUnit("requests"), (BudgetLimitKind) int.MaxValue));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void BudgetLimit_WhenCopiedDimensionIsDefault_RejectsCopy()
    {
        var limit = Limit();
        var exception = Should.Throw<ArgumentNullException>(() => limit with { Dimension = default });
        exception.ParamName.ShouldBe("Dimension");
    }

    [Fact]
    public void BudgetLimit_WhenCopiedUnitIsDefault_RejectsCopy()
    {
        var limit = Limit();
        var exception = Should.Throw<ArgumentNullException>(() => limit with { Unit = default });
        exception.ParamName.ShouldBe("Unit");
    }

    [Fact]
    public void BudgetLimit_WhenCopiedValueIsNegative_RejectsCopy()
    {
        var limit = Limit();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => limit with { Value = -1m });
        exception.ParamName.ShouldBe("Value");
    }

    [Fact]
    public void BudgetLimit_WhenCopiedKindIsUndefined_RejectsCopy()
    {
        var limit = Limit();
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => limit with { Kind = (BudgetLimitKind) int.MaxValue });
        exception.ParamName.ShouldBe("Kind");
    }

    private static BudgetLimit Limit() => new(new BudgetDimension("tests.requests"), 1m, new BudgetUnit("requests"), BudgetLimitKind.Hard);
}
