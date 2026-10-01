// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Retrieval;

/// <summary>Verifies <see cref="RetrievalSummary"/> constraints.</summary>
public sealed class RetrievalSummaryTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryCount()
    {
        var summary = new RetrievalSummary(10, 1, 2, 3, 4, 5, 6);

        summary.Searched.ShouldBe(10);
        summary.OmittedStale.ShouldBe(1);
        summary.OmittedUnauthorized.ShouldBe(2);
        summary.OmittedDuplicate.ShouldBe(3);
        summary.OmittedByBudget.ShouldBe(4);
        summary.SourcesUnavailable.ShouldBe(5);
        summary.DeletionGeneration.ShouldBe(6);
    }

    [Theory]
    [InlineData(0, "searched")]
    [InlineData(1, "omittedStale")]
    [InlineData(2, "omittedUnauthorized")]
    [InlineData(3, "omittedDuplicate")]
    [InlineData(4, "omittedByBudget")]
    [InlineData(5, "sourcesUnavailable")]
    public void Constructor_WhenACountIsNegative_ThrowsArgumentOutOfRangeException(int position, string parameter)
    {
        var counts = new int[6];
        counts[position] = -1;

        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalSummary(counts[0], counts[1], counts[2], counts[3], counts[4], counts[5], null)).ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenGenerationIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RetrievalSummary(0, 0, 0, 0, 0, 0, -1)).ParamName.ShouldBe("deletionGeneration");
}
