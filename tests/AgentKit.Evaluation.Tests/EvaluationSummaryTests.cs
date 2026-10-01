// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationSummaryTests
{
    [Fact]
    public void Constructor_WhenACountIsNegative_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationSummary(-1, 0, 0, 0, 0)).ParamName.ShouldBe("passed");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationSummary(0, -1, 0, 0, 0)).ParamName.ShouldBe("failed");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationSummary(0, 0, -1, 0, 0)).ParamName.ShouldBe("inconclusive");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationSummary(0, 0, 0, -1, 0)).ParamName.ShouldBe("notEvaluated");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationSummary(0, 0, 0, 0, -1)).ParamName.ShouldBe("notStarted");
    }

    [Fact]
    public void Recorded_WhenCountsAreSupplied_ExcludesNotStartedRepetitions() =>
        new EvaluationSummary(1, 2, 3, 4, 5).Recorded.ShouldBe(10);
}
