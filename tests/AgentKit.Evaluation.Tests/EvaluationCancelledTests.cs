// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationCancelledTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSummaryIsBlank_ThrowsArgumentException(string? summary) =>
        Should.Throw<ArgumentException>(() => new EvaluationCancelled(summary!)).ParamName.ShouldBe("summary");

    [Fact]
    public void Constructor_WhenSummaryIsSupplied_ExposesVariantNameAndNoScore()
    {
        var outcome = new EvaluationCancelled("because");

        outcome.Name.ShouldBe("cancelled");
        outcome.Summary.ShouldBe("because");
        outcome.Score.ShouldBeNull();
        _ = outcome.ShouldBeAssignableTo<EvaluationOutcome>();
    }

    [Fact]
    public void Constructor_WhenEvidenceIsSupplied_RecordsItAndDistinguishesVariants()
    {
        var outcome = new EvaluationCancelled("because", [new EvaluationEvidence("n", "v")]);

        outcome.Evidence.ShouldBe([new EvaluationEvidence("n", "v")]);
        outcome.ShouldNotBe(new EvaluationCancelled("because"));
        new EvaluationCancelled("because").Evidence.ShouldBeEmpty();
    }
}
