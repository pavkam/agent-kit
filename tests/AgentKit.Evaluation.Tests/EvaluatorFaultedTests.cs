// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluatorFaultedTests
{
    [Theory]
    [InlineData(null, "summary", "errorType")]
    [InlineData(" ", "summary", "errorType")]
    [InlineData("System.InvalidOperationException", "", "summary")]
    public void Constructor_WhenTextIsBlank_ThrowsWithTheParameterName(string? errorType, string summary, string parameter) =>
        Should.Throw<ArgumentException>(() => new EvaluatorFaulted(errorType!, summary)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesTheFailureWithoutAScore()
    {
        var outcome = new EvaluatorFaulted("System.InvalidOperationException", "evaluator threw");

        outcome.Name.ShouldBe("evaluator_failed");
        outcome.ErrorType.ShouldBe("System.InvalidOperationException");
        outcome.Score.ShouldBeNull();
    }
}
