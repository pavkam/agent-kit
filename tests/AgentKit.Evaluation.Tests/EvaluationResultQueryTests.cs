// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultQueryTests
{
    [Fact]
    public void Constructor_WhenRunIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultQuery(default, 10)).ParamName.ShouldBe("evaluationRunId");

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(EvaluationResultQuery.MaximumPageSize + 1)]
    public void Constructor_WhenPageSizeIsOutOfRange_ThrowsArgumentOutOfRangeException(int pageSize) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationResultQuery(EvaluationTestData.RunId, pageSize)).ParamName.ShouldBe("pageSize");

    [Theory]
    [InlineData(1)]
    [InlineData(EvaluationResultQuery.MaximumPageSize)]
    public void Constructor_WhenPageSizeIsAtABoundary_AcceptsIt(int pageSize)
    {
        var query = new EvaluationResultQuery(EvaluationTestData.RunId, pageSize, new EvaluationResultCursor(2, 1));

        query.PageSize.ShouldBe(pageSize);
        query.After.ShouldBe(new EvaluationResultCursor(2, 1));
    }
}
