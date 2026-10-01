// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class ModelJudgeBudgetTests
{
    [Theory]
    [InlineData(0, null, "maximumCalls")]
    [InlineData(-1, null, "maximumCalls")]
    [InlineData(1, 0L, "maximumTokens")]
    [InlineData(1, -5L, "maximumTokens")]
    public void Constructor_WhenABoundIsNotPositive_ThrowsArgumentOutOfRangeException(int calls, long? tokens, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelJudgeBudget(calls, tokens)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenTokensAreOmitted_LeavesThemUnbounded()
    {
        var budget = new ModelJudgeBudget(5);

        budget.MaximumCalls.ShouldBe(5);
        budget.MaximumTokens.ShouldBeNull();
    }
}
