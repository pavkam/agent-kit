// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationResultsReadTests
{
    [Fact]
    public void Constructor_WhenResultsAreDefaultOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluationResultsRead(default, null)).ParamName.ShouldBe("results");
        Should.Throw<ArgumentException>(() => new EvaluationResultsRead([null!], null)).ParamName.ShouldBe("results");
    }

    [Fact]
    public void Equals_WhenResultsMatchByValue_IsEqualAndHashesAlike()
    {
        var left = new EvaluationResultsRead([EvaluationTestData.Result()], new EvaluationResultCursor(0, 1));
        var right = new EvaluationResultsRead([EvaluationTestData.Result()], new EvaluationResultCursor(0, 1));

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(new EvaluationResultsRead([EvaluationTestData.Result()], null));
    }
}
