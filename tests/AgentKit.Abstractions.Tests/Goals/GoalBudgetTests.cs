// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalBudget constraints.</summary>
public sealed class GoalBudgetTests
{
    [Fact]
    public void Constructor_WhenValuesAreValid_RetainsThem()
    {
        var budget = new GoalBudget(2, 3, 0);

        (budget.MaximumTurns, budget.MaximumToolCalls, budget.MaximumChildren).ShouldBe((2, 3, 0));
    }

    [Theory]
    [InlineData(0, 1, 0, "maximumTurns")]
    [InlineData(-1, 1, 0, "maximumTurns")]
    [InlineData(1, 0, 0, "maximumToolCalls")]
    [InlineData(1, 1, -1, "maximumChildren")]
    public void Constructor_WhenAValueIsOutOfRange_ThrowsArgumentOutOfRangeExceptionNamingIt(int turns, int calls, int children, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalBudget(turns, calls, children)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Equality_WhenValuesMatch_IsStructural() => new GoalBudget(1, 2, 3).ShouldBe(new GoalBudget(1, 2, 3));
}
