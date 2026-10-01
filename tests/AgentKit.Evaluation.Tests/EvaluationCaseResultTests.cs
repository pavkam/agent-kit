// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationCaseResultTests
{
    private static EvaluationCaseResult Build(
        EvaluationRunId? runId = null,
        string plan = "plan",
        long version = 1,
        string caseId = "c",
        int ordinal = 0,
        int repetition = 1,
        EvaluationCaseDisposition disposition = EvaluationCaseDisposition.Evaluated,
        TimeSpan? latency = null,
        string? trace = "trace",
        EvaluationRunRecord? run = null,
        EvaluationRunManifest? manifest = null,
        EvaluationUsageSummary? usage = null,
        ImmutableArray<EvaluatorResult>? evaluators = null,
        ImmutableArray<EvaluationDiagnostic>? diagnostics = null,
        bool omitRun = false) =>
        new(
            runId ?? EvaluationTestData.RunId,
            new EvaluationPlanId(plan),
            new EvaluationPlanVersion(version),
            new EvaluationCaseId(caseId),
            ordinal,
            repetition,
            disposition,
            DateTimeOffset.UnixEpoch,
            latency ?? TimeSpan.Zero,
            trace,
            omitRun ? null : run ?? EvaluationTestData.Run(),
            manifest ?? EvaluationTestData.Manifest(),
            usage ?? EvaluationUsageSummary.None,
            null,
            evaluators ?? [],
            diagnostics ?? []);

    private static EvaluatorResult Result(EvaluationOutcome outcome) =>
        new(new EvaluatorKey("k"), new EvaluatorVersion(1), outcome, TimeSpan.Zero);

    [Fact]
    public void Constructor_WhenIdentitiesAreDefault_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Build(runId: default(EvaluationRunId))).ParamName.ShouldBe("evaluationRunId");
        Should.Throw<ArgumentException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, default, new EvaluationPlanVersion(1), new EvaluationCaseId("c"), 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, EvaluationTestData.Manifest(), EvaluationUsageSummary.None, null, [], []))
            .ParamName.ShouldBe("planId");
        Should.Throw<ArgumentException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), default, 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, EvaluationTestData.Manifest(), EvaluationUsageSummary.None, null, [], []))
            .ParamName.ShouldBe("caseId");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, new EvaluationPlanId("p"), default, new EvaluationCaseId("c"), 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, EvaluationTestData.Manifest(), EvaluationUsageSummary.None, null, [], []))
            .ParamName.ShouldBe("planVersion");
    }

    [Theory]
    [InlineData(-1, 1, "caseOrdinal")]
    [InlineData(0, 0, "repetition")]
    [InlineData(0, -2, "repetition")]
    public void Constructor_WhenOrdinalOrRepetitionIsOutOfRange_ThrowsArgumentOutOfRangeException(int ordinal, int repetition, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(ordinal: ordinal, repetition: repetition)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenDispositionIsUndefinedOrLatencyIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Build(disposition: (EvaluationCaseDisposition) 99)).ParamName.ShouldBe("disposition");
        Should.Throw<ArgumentOutOfRangeException>(() => Build(latency: TimeSpan.FromTicks(-1))).ParamName.ShouldBe("latency");
    }

    [Fact]
    public void Constructor_WhenTraceIdIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => Build(trace: " ")).ParamName.ShouldBe("traceId");

    [Fact]
    public void Constructor_WhenEvaluatedWithoutARunRecord_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => Build(omitRun: true)).ParamName.ShouldBe("run");

    [Fact]
    public void Constructor_WhenRunWasRejected_AcceptsNoRunRecordAndNoTrace()
    {
        var result = Build(disposition: EvaluationCaseDisposition.RunRejected, omitRun: true, trace: null);

        result.Run.ShouldBeNull();
        result.TraceId.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenReferencesAreNullOrArraysAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), new EvaluationCaseId("c"), 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, null!, EvaluationUsageSummary.None, null, [], []))
            .ParamName.ShouldBe("manifest");
        Should.Throw<ArgumentNullException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), new EvaluationCaseId("c"), 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, EvaluationTestData.Manifest(), null!, null, [], []))
            .ParamName.ShouldBe("usage");
        Should.Throw<ArgumentException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), new EvaluationCaseId("c"), 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, EvaluationTestData.Manifest(), EvaluationUsageSummary.None, null, default, []))
            .ParamName.ShouldBe("evaluators");
        Should.Throw<ArgumentException>(() => new EvaluationCaseResult(EvaluationTestData.RunId, new EvaluationPlanId("p"), new EvaluationPlanVersion(1), new EvaluationCaseId("c"), 0, 1, EvaluationCaseDisposition.RunRejected, DateTimeOffset.UnixEpoch, TimeSpan.Zero, null, null, EvaluationTestData.Manifest(), EvaluationUsageSummary.None, null, [], default))
            .ParamName.ShouldBe("diagnostics");
        Should.Throw<ArgumentException>(() => Build(evaluators: [null!])).ParamName.ShouldBe("evaluators");
        Should.Throw<ArgumentException>(() => Build(diagnostics: [null!])).ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void Verdict_WhenTheRepetitionWasNotEvaluated_IsNotEvaluatedWhateverTheEvaluatorsSay()
    {
        var result = Build(disposition: EvaluationCaseDisposition.Cancelled, omitRun: true, evaluators: [Result(new EvaluationPassed(null, "ok"))]);

        result.Verdict.ShouldBe(EvaluationVerdict.NotEvaluated);
    }

    [Fact]
    public void Verdict_WhenAnyEvaluatorFailed_IsFailedEvenIfOthersPassed()
    {
        var result = Build(evaluators: [Result(new EvaluationPassed(null, "ok")), Result(new EvaluationFailed(null, "bad"))]);

        result.Verdict.ShouldBe(EvaluationVerdict.Failed);
    }

    [Theory]
    [InlineData(typeof(EvaluationInconclusive))]
    [InlineData(typeof(EvaluationCancelled))]
    [InlineData(typeof(EvaluationUnsupported))]
    [InlineData(typeof(EvaluatorFaulted))]
    public void Verdict_WhenNoEvaluatorFailedButOneIsUndecided_IsInconclusive(Type undecided)
    {
        EvaluationOutcome outcome = undecided == typeof(EvaluationInconclusive) ? new EvaluationInconclusive(null, "u")
            : undecided == typeof(EvaluationCancelled) ? new EvaluationCancelled("u")
            : undecided == typeof(EvaluationUnsupported) ? new EvaluationUnsupported("u")
            : new EvaluatorFaulted("T", "u");

        var result = Build(evaluators: [Result(new EvaluationPassed(null, "ok")), Result(outcome)]);

        result.Verdict.ShouldBe(EvaluationVerdict.Inconclusive);
    }

    [Fact]
    public void Verdict_WhenEvaluatorsPassedOrSkipped_IsPassedOnlyWhenOnePassed()
    {
        Build(evaluators: [Result(new EvaluationPassed(null, "ok")), Result(new EvaluationSkipped("pre"))]).Verdict.ShouldBe(EvaluationVerdict.Passed);
        Build(evaluators: [Result(new EvaluationSkipped("pre"))]).Verdict.ShouldBe(EvaluationVerdict.NotEvaluated);
        Build(evaluators: []).Verdict.ShouldBe(EvaluationVerdict.NotEvaluated);
    }

    [Fact]
    public void Equals_WhenEveryMemberMatchesByValue_IsEqualAndHashesAlike()
    {
        var left = EvaluationTestData.Result();
        var right = EvaluationTestData.Result();

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(EvaluationTestData.Result(repetition: 2));
        left.ShouldNotBe(EvaluationTestData.Result(evaluators: [EvaluationTestData.Passed("other")]));
    }
}
