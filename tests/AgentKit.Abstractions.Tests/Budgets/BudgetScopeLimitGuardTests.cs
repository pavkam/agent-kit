// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Budgets;

public sealed class BudgetScopeLimitGuardTests
{
    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenArrayIsDefault_ThrowsExactArgumentExceptionWithInferredParameterName()
    {
        ImmutableArray<BudgetLimit> limits = default;

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenArrayContainsNull_ThrowsExactArgumentExceptionWithExplicitParameterName()
    {
        ImmutableArray<BudgetLimit> limits = [null!];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits, "scopeLimits"));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("scopeLimits");
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenDimensionRepeats_ThrowsExactArgumentException()
    {
        var dimension = new BudgetDimension("tests.requests");
        ImmutableArray<BudgetLimit> limits =
        [
            new BudgetLimit(dimension, 0m, new BudgetUnit("requests"), BudgetLimitKind.Hard),
            new BudgetLimit(dimension, 1m, new BudgetUnit("requests"), BudgetLimitKind.Soft),
        ];

        var exception = Should.Throw<ArgumentException>(
            () => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("limits");
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenArrayIsEmpty_DoesNotThrow()
    {
        Should.NotThrow(() =>
            ArgumentException.ThrowIfInvalidBudgetScopeLimits([]));
    }

    [Fact]
    public void ThrowIfInvalidBudgetScopeLimits_WhenDistinctZeroBoundaryLimitsAreValid_DoesNotThrow()
    {
        ImmutableArray<BudgetLimit> limits =
        [
            new BudgetLimit(
                new BudgetDimension("tests.requests"), 0m, new BudgetUnit("requests"), BudgetLimitKind.Hard),
            new BudgetLimit(
                new BudgetDimension("tests.tokens"), 0m, new BudgetUnit("tokens"), BudgetLimitKind.Soft),
        ];

        Should.NotThrow(() => ArgumentException.ThrowIfInvalidBudgetScopeLimits(limits));
    }
}
