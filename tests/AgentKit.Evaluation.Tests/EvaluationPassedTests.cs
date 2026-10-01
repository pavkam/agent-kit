// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationPassedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSummaryIsBlank_ThrowsArgumentException(string? summary) =>
        Should.Throw<ArgumentException>(() => new EvaluationPassed(null, summary!)).ParamName.ShouldBe("summary");

    [Fact]
    public void Constructor_WhenScoreIsSupplied_ExposesVariantNameSummaryAndScore()
    {
        var score = new EvaluationScore(0.75, 3, 0.1);

        var outcome = new EvaluationPassed(score, "because");

        outcome.Name.ShouldBe("passed");
        outcome.Summary.ShouldBe("because");
        outcome.Score.ShouldBe(score);
        _ = outcome.ShouldBeAssignableTo<EvaluationOutcome>();
    }

    [Fact]
    public void Constructor_WhenScoreIsOmitted_LeavesScoreNull() =>
        new EvaluationPassed(null, "because").Score.ShouldBeNull();

    [Fact]
    public void Constructor_WhenEvidenceIsOmittedDefaultOrContainsNull_ExposesEmptyEvidenceOrThrows()
    {
        new EvaluationPassed(null, "because").Evidence.IsDefault.ShouldBeFalse();
        new EvaluationPassed(null, "because").Evidence.ShouldBeEmpty();
        new EvaluationPassed(null, "because", [new EvaluationEvidence("n", "v")]).Evidence.ShouldBe([new EvaluationEvidence("n", "v")]);
        Should.Throw<ArgumentException>(() => new EvaluationPassed(null, "because", [null!])).ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void Equals_WhenSummaryScoreAndEvidenceMatch_IsEqualAndHashesAlike()
    {
        var left = new EvaluationPassed(EvaluationScore.Certain(true), "s", [new EvaluationEvidence("n", "v")]);
        var right = new EvaluationPassed(EvaluationScore.Certain(true), "s", [new EvaluationEvidence("n", "v")]);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(new EvaluationPassed(EvaluationScore.Certain(true), "s", [new EvaluationEvidence("n", "w")]));
    }
}
