// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalBudget"/> bounds and narrowing.</summary>
public sealed class RetrievalBudgetTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryBound()
    {
        var budget = new RetrievalBudget(3, 400, 50);

        budget.MaximumItems.ShouldBe(3);
        budget.MaximumBytes.ShouldBe(400);
        budget.MaximumTokens.ShouldBe(50);
    }

    [Theory]
    [InlineData(0, 1, 1, "maximumItems")]
    [InlineData(1, 0, 1, "maximumBytes")]
    [InlineData(1, 1, 0, "maximumTokens")]
    [InlineData(-1, 1, 1, "maximumItems")]
    public void Constructor_WhenABoundIsNotPositive_ThrowsArgumentOutOfRangeException(int items, int bytes, int tokens, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalBudget(items, bytes, tokens)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Narrow_WhenBoundsDiffer_TakesTheSmallerOfEach() =>
        new RetrievalBudget(10, 100, 5).Narrow(new RetrievalBudget(4, 500, 9)).ShouldBe(new RetrievalBudget(4, 100, 5));

    [Fact]
    public void Narrow_WhenOtherIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalBudget(1, 1, 1).Narrow(null!)).ParamName.ShouldBe("other");
}
