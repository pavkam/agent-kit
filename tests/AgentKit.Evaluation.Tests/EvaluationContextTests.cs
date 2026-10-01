// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationContextTests
{
    private static EvaluationContext Build(
        EvaluationRunId? runId = null,
        EvaluationPlanId? planId = null,
        long version = 1,
        EvaluationCase? evaluationCase = null,
        int repetition = 1,
        AgentRunResult<ValidatedOutput>? result = null,
        EvaluationRunManifest? manifest = null,
        EvaluationUsageSummary? usage = null,
        TimeSpan? latency = null) =>
        new(
            runId ?? EvaluationTestData.RunId,
            planId ?? new EvaluationPlanId("p"),
            new EvaluationPlanVersion(version),
            evaluationCase ?? EvaluationTestData.Case(),
            repetition,
            result ?? EvaluationTestData.Finished(),
            manifest ?? EvaluationTestData.Manifest(),
            usage ?? EvaluationUsageSummary.None,
            latency ?? TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenIdentityIsDefault_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Build(runId: default(EvaluationRunId))).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentException>(() => Build(planId: default(EvaluationPlanId))).ParamName.ShouldBe("planId");
        Should.Throw<ArgumentOutOfRangeException>(() => Build(version: 0 + 1, repetition: 0)).ParamName.ShouldBe("repetition");
    }

    [Fact]
    public void Constructor_WhenAReferenceIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new EvaluationContext(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), null!, 1, EvaluationTestData.Finished(), EvaluationTestData.Manifest(), EvaluationUsageSummary.None, TimeSpan.Zero))
            .ParamName.ShouldBe("evaluationCase");
        Should.Throw<ArgumentNullException>(() => new EvaluationContext(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), EvaluationTestData.Case(), 1, null!, EvaluationTestData.Manifest(), EvaluationUsageSummary.None, TimeSpan.Zero))
            .ParamName.ShouldBe("result");
        Should.Throw<ArgumentNullException>(() => new EvaluationContext(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), EvaluationTestData.Case(), 1, EvaluationTestData.Finished(), null!, EvaluationUsageSummary.None, TimeSpan.Zero))
            .ParamName.ShouldBe("manifest");
        Should.Throw<ArgumentNullException>(() => new EvaluationContext(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), EvaluationTestData.Case(), 1, EvaluationTestData.Finished(), EvaluationTestData.Manifest(), null!, TimeSpan.Zero))
            .ParamName.ShouldBe("usage");
    }

    [Fact]
    public void Constructor_WhenLatencyIsNegative_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(latency: TimeSpan.FromTicks(-1))).ParamName.ShouldBe("latency");

    [Fact]
    public void AssistantText_WhenTheRunProducedAssistantMessages_ConcatenatesOnlyAssistantText()
    {
        var finished = EvaluationTestData.Finished(messages: [EvaluationTestData.AssistantText("one "), EvaluationTestData.AssistantText("two")]);

        var context = Build(result: finished);

        context.Finished.ShouldBeSameAs(finished);
        context.AssistantText.ShouldBe("one two");
    }

    [Fact]
    public void AssistantText_WhenTheRunWasRejected_IsEmptyAndFinishedIsNull()
    {
        var context = Build(result: EvaluationTestData.Rejected());

        context.Finished.ShouldBeNull();
        context.AssistantText.ShouldBeEmpty();
    }
}
