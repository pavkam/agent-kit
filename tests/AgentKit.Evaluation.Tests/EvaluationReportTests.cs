// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

public sealed class EvaluationReportTests
{
    private static readonly DateTimeOffset _start = DateTimeOffset.UnixEpoch;

    private static EvaluationReport Build(
        EvaluationRunId? runId = null,
        EvaluationReportStatus status = EvaluationReportStatus.Completed,
        ImmutableArray<EvaluationCaseResult>? results = null,
        int notStarted = 0,
        DateTimeOffset? completed = null) =>
        new(
            runId ?? EvaluationTestData.RunId,
            new EvaluationPlanId("plan"),
            new EvaluationPlanVersion(1),
            _start,
            completed ?? _start.AddSeconds(5),
            status,
            results ?? [EvaluationTestData.Result()],
            notStarted,
            [],
            []);

    [Fact]
    public void Constructor_WhenIdentityIsDefault_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Build(runId: default(EvaluationRunId), results: [])).ParamName.ShouldBe("runId");
        Should.Throw<ArgumentException>(() => new EvaluationReport(EvaluationTestData.RunId, default, new EvaluationPlanVersion(1), _start, _start, EvaluationReportStatus.Completed, [], 0, [], []))
            .ParamName.ShouldBe("planId");
        Should.Throw<ArgumentOutOfRangeException>(() => new EvaluationReport(EvaluationTestData.RunId, new EvaluationPlanId("plan"), default, _start, _start, EvaluationReportStatus.Completed, [], 0, [], []))
            .ParamName.ShouldBe("planVersion");
    }

    [Fact]
    public void Constructor_WhenCompletionPrecedesStart_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Build(completed: _start.AddTicks(-1))).ParamName.ShouldBe("completedAt");

    [Fact]
    public void Constructor_WhenStatusIsUndefinedOrNotStartedIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Build(status: (EvaluationReportStatus) 42)).ParamName.ShouldBe("status");
        Should.Throw<ArgumentOutOfRangeException>(() => Build(notStarted: -1)).ParamName.ShouldBe("notStartedCaseRuns");
    }

    [Fact]
    public void Constructor_WhenArraysAreDefaultOrContainNull_ThrowsArgumentException()
    {
        Should.Throw<ArgumentException>(() => new EvaluationReport(EvaluationTestData.RunId, new EvaluationPlanId("plan"), new EvaluationPlanVersion(1), _start, _start, EvaluationReportStatus.Completed, default, 0, [], []))
            .ParamName.ShouldBe("results");
        Should.Throw<ArgumentException>(() => Build(results: [null!])).ParamName.ShouldBe("results");
        Should.Throw<ArgumentException>(() => new EvaluationReport(EvaluationTestData.RunId, new EvaluationPlanId("plan"), new EvaluationPlanVersion(1), _start, _start, EvaluationReportStatus.Completed, [], 0, default, []))
            .ParamName.ShouldBe("storeResults");
        Should.Throw<ArgumentException>(() => new EvaluationReport(EvaluationTestData.RunId, new EvaluationPlanId("plan"), new EvaluationPlanVersion(1), _start, _start, EvaluationReportStatus.Completed, [], 0, [], default))
            .ParamName.ShouldBe("exportResults");
    }

    [Fact]
    public void Constructor_WhenAResultBelongsToAnotherRunOrPlan_ThrowsArgumentException()
    {
        var otherRun = new EvaluationRunId(Guid.NewGuid());

        Should.Throw<ArgumentException>(() => Build(results: [EvaluationTestData.Result(runId: otherRun)])).ParamName.ShouldBe("results");
        Should.Throw<ArgumentException>(() => Build(results: [EvaluationTestData.Result(plan: "other")])).ParamName.ShouldBe("results");
        Should.Throw<ArgumentException>(() => Build(results: [EvaluationTestData.Result(planVersion: 2)])).ParamName.ShouldBe("results");
    }

    [Fact]
    public void Summary_WhenResultsHaveMixedVerdicts_CountsEachVerdictAndTheNotStartedRepetitions()
    {
        var failed = EvaluationTestData.Result(0, evaluators: [new EvaluatorResult(new EvaluatorKey("k"), new EvaluatorVersion(1), new EvaluationFailed(null, "x"), TimeSpan.Zero)]);
        var passed = EvaluationTestData.Result(1);
        var inconclusive = EvaluationTestData.Result(2, evaluators: [new EvaluatorResult(new EvaluatorKey("k"), new EvaluatorVersion(1), new EvaluationInconclusive(null, "x"), TimeSpan.Zero)]);
        var cancelled = EvaluationTestData.Result(3, disposition: EvaluationCaseDisposition.Cancelled, evaluators: []);

        var summary = Build(status: EvaluationReportStatus.Cancelled, results: [failed, passed, inconclusive, cancelled], notStarted: 6).Summary;

        summary.ShouldBe(new EvaluationSummary(1, 1, 1, 1, 6));
        summary.Recorded.ShouldBe(4);
    }

    [Fact]
    public void WithExportResults_WhenExportsAreRecorded_ReturnsACopyWithOnlyThatMemberChanged()
    {
        var report = Build();
        var exports = ImmutableArray.Create(new EvaluationExportRecord(new EvaluationReportExporterKey("e"), new EvaluationExported()));

        var updated = report.WithExportResults(exports);

        updated.ExportResults.ShouldBe(exports);
        updated.Results.ShouldBe(report.Results);
        updated.ShouldNotBe(report);
        report.ExportResults.ShouldBeEmpty();
    }

    [Fact]
    public void Equals_WhenEveryMemberMatchesByValue_IsEqualAndHashesAlike()
    {
        var left = Build();
        var right = Build();

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        left.ShouldNotBe(Build(notStarted: 1));
    }
}
