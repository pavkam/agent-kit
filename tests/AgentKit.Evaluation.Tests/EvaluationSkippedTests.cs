// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationSkippedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSummaryIsBlank_ThrowsArgumentException(string? summary) =>
        Should.Throw<ArgumentException>(() => new EvaluationSkipped(summary!)).ParamName.ShouldBe("summary");

    [Fact]
    public void Constructor_WhenSummaryIsSupplied_ExposesVariantNameAndNoScore()
    {
        var outcome = new EvaluationSkipped("because");

        outcome.Name.ShouldBe("skipped");
        outcome.Summary.ShouldBe("because");
        outcome.Score.ShouldBeNull();
        _ = outcome.ShouldBeAssignableTo<EvaluationOutcome>();
    }

    [Fact]
    public void Constructor_WhenEvidenceIsSupplied_RecordsItAndDistinguishesVariants()
    {
        var outcome = new EvaluationSkipped("because", [new EvaluationEvidence("n", "v")]);

        outcome.Evidence.ShouldBe([new EvaluationEvidence("n", "v")]);
        outcome.ShouldNotBe(new EvaluationSkipped("because"));
        new EvaluationSkipped("because").Evidence.ShouldBeEmpty();
    }
}
