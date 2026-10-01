// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalBudgetDecision"/> constraints.</summary>
public sealed class RetrievalBudgetDecisionTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesSelectionAndOmittedCount()
    {
        var decision = new RetrievalBudgetDecision([], 3);

        decision.Selected.ShouldBeEmpty();
        decision.Omitted.ShouldBe(3);
    }

    [Fact]
    public void Constructor_WhenSelectionIsDefaultOrContainsNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new RetrievalBudgetDecision(default, 0)).ParamName.ShouldBe("selected");
        Should.Throw<ArgumentException>(() => new RetrievalBudgetDecision([null!], 0)).ParamName.ShouldBe("selected");
    }

    [Fact]
    public void Constructor_WhenOmittedIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalBudgetDecision([], -1)).ParamName.ShouldBe("omitted");
}
