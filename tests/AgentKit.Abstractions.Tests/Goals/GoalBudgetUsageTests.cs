// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies GoalBudgetUsage constraints.</summary>
public sealed class GoalBudgetUsageTests
{
    [Fact]
    public void None_WhenRead_ReportsZeroConsumptionAndUnknownTokens()
    {
        GoalBudgetUsage.None.Turns.ShouldBe(0);
        GoalBudgetUsage.None.TotalTokens.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, 0, 0, null, "turns")]
    [InlineData(0, -1, 0, null, "toolCalls")]
    [InlineData(0, 0, -1, null, "children")]
    [InlineData(0, 0, 0, -1L, "totalTokens")]
    public void Constructor_WhenAValueIsNegative_ThrowsArgumentOutOfRangeExceptionNamingIt(int turns, int calls, int children, long? tokens, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GoalBudgetUsage(turns, calls, children, tokens)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenTokensAreReportedAsZero_KeepsZeroDistinctFromUnknown()
    {
        new GoalBudgetUsage(1, 1, 0, 0).TotalTokens.ShouldBe(0);
        new GoalBudgetUsage(1, 1, 0).TotalTokens.ShouldBeNull();
    }
}
