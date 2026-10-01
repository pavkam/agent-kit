// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalBudgetRequest"/> constraints.</summary>
public sealed class RetrievalBudgetRequestTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesBudgetAndCandidates() =>
        new RetrievalBudgetRequest(new RetrievalBudget(1, 1, 1), []).Budget.ShouldBe(new RetrievalBudget(1, 1, 1));

    [Fact]
    public void Constructor_WhenBudgetIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new RetrievalBudgetRequest(null!, [])).ParamName.ShouldBe("budget");

    [Fact]
    public void Constructor_WhenCandidatesAreDefaultOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new RetrievalBudgetRequest(new RetrievalBudget(1, 1, 1), default)).ParamName.ShouldBe("candidates");
        Should.Throw<ArgumentException>(() => new RetrievalBudgetRequest(new RetrievalBudget(1, 1, 1), [null!])).ParamName.ShouldBe("candidates");
    }
}
