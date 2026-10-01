// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

using System.Collections.Concurrent;
using System.Collections.Frozen;

using AgentKit.Tests;

public sealed class EvaluationRunnerTests
{
    private const string _canaryInput = "CANARY-PROMPT-7f3a";
    private const string _canaryReply = "CANARY-REPLY-91bc";

    private static readonly AgentRunOutcome _cancelledOutcome =
        new RunCancelled(new CancellationReason(RunResultTestData.Error(AgentErrorCodes.Cancelled)));

    private static EvaluationCase Case(string id, params string[] evaluators) =>
        EvaluationTestData.Case(id, evaluators: [.. evaluators.Select(static key => new EvaluatorReference(new EvaluatorKey(key)))]);

    private static string Key([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        $"{name}-{Guid.NewGuid():N}";

    private static async Task<T> WithTimeout<T>(Task<T> task) =>
        await task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    private static async Task WithTimeout(Task task) =>
        await task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

    private sealed class SequentialRunIds: IIdentifierGenerator<EvaluationRunId>
    {
        private int _next;

        public EvaluationRunId Create() => new(new Guid(Interlocked.Increment(ref _next), 0, 0, [0, 0, 0, 0, 0, 0, 0, 9]));
    }

    private sealed class ThrowingStore: IEvaluationResultStore
    {
        public ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("store secret");

        public ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("store secret");
    }

    [Fact]
    public async Task RunAsync_WhenPlanIsNull_ThrowsArgumentNullException()
    {
        await using var harness = await EvaluationHarness.CreateAsync();

        var exception = await Should.ThrowAsync<ArgumentNullException>(() => harness.Runner.RunAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("plan");
    }

    [Fact]
    public async Task RunAsync_WhenPlanIsValid_RunsEveryCaseThroughTheEngineAndRecordsTypedIdentities()
    {
        var key = Key();
        var evaluator = new ScriptedEvaluator(key);
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>(key, evaluator));

        var report = await harness.Runner.RunAsync(
            EvaluationTestData.Plan([Case("a", key), Case("b", key)]),
            TestContext.Current.CancellationToken);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.PlanId.ShouldBe(new EvaluationPlanId("plan"));
        report.PlanVersion.ShouldBe(new EvaluationPlanVersion(1));
        report.Results.Select(static r => r.CaseId.Value).ShouldBe(["a", "b"]);
        report.Results.Select(static r => r.CaseOrdinal).ShouldBe([0, 1]);
        report.Results.ShouldAllBe(static r => r.Disposition == EvaluationCaseDisposition.Evaluated && r.Verdict == EvaluationVerdict.Passed);
        report.Results.Select(static r => r.Run!.RunId).ToHashSet().SetEquals(harness.Loop.Requests.Select(static r => r.RunId)).ShouldBeTrue();
        report.Results.Select(static r => r.Run!.SessionId).Distinct().Count().ShouldBe(2);
        report.Results.ShouldAllBe(static r => r.Run!.Outcome == "succeeded" && r.Run.Settlement == "completed");
        report.Results[0].Manifest.AgentId.ShouldBe(EvaluationTestData.Agent);
        report.Results[0].Manifest.SessionProfile.ShouldBe(EvaluationTestData.SessionProfile);
        report.Results[0].Manifest.ModelCandidates.ShouldBe(["chat"]);
        report.Results[0].Evaluators.Single().Version.ShouldBe(new EvaluatorVersion(1));
        report.Summary.ShouldBe(new EvaluationSummary(2, 0, 0, 0, 0));
        evaluator.Contexts.Count.ShouldBe(2);
        evaluator.Contexts.ShouldAllBe(static c => c.AssistantText == "ok" && c.PlanId == new EvaluationPlanId("plan"));
    }

    [Fact]
    public async Task RunAsync_WhenCasesAreRepeated_UsesAFreshSessionPerRepetitionAndNumbersThemFromOne()
    {
        await using var harness = await EvaluationHarness.CreateAsync(
            services => services.Configure<EvaluationOptions>(static o => o.MaximumRepetitions = 3));

        var report = await harness.Runner.RunAsync(
            EvaluationTestData.Plan(
                [Case("a"), Case("b")],
                new EvaluationExecutionPolicy(1, 3, null, null, null, false, false)),
            TestContext.Current.CancellationToken);

        report.Results.Select(static r => (r.CaseId.Value, r.Repetition)).ShouldBe(
            [("a", 1), ("a", 2), ("a", 3), ("b", 1), ("b", 2), ("b", 3)]);
        harness.Sessions.CreateRequests.Count.ShouldBe(6);
        harness.Sessions.CreateRequests.Select(static r => r.IdempotencyKey).Distinct().Count().ShouldBe(6);
        harness.Loop.Requests.Select(static r => r.SessionId).Distinct().Count().ShouldBe(6);
    }

    [Fact]
    public async Task RunAsync_WhenConcurrencyIsBounded_NeverRunsMoreCasesAtOnceThanThePlanAllows()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var loop = new GatedAgentLoop
        {
            OnEntered = async (request, token) =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                {
                    _ = twoEntered.TrySetResult();
                }

                await gate.Task.WaitAsync(token);
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(loop: loop);
        var plan = EvaluationTestData.Plan(
            [.. Enumerable.Range(0, 6).Select(index => Case($"c{index}"))],
            new EvaluationExecutionPolicy(2, 1, null, null, null, false, false));

        var run = harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);
        await WithTimeout(twoEntered.Task);
        _ = gate.TrySetResult();
        var report = await WithTimeout(run);

        report.Results.Length.ShouldBe(6);
        loop.PeakConcurrency.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenCompletionOrderDiffersFromPlanOrder_StillReportsDeterministicallyInPlanOrder()
    {
        const int cases = 4;
        var allEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = Enumerable.Range(0, cases).Select(static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var arrival = -1;
        var completionOrder = new ConcurrentQueue<int>();
        var loop = new GatedAgentLoop
        {
            OnEntered = async (request, token) =>
            {
                var mine = Interlocked.Increment(ref arrival);
                if (mine == cases - 1)
                {
                    _ = allEntered.TrySetResult();
                }

                await allEntered.Task.WaitAsync(token);
                if (mine < cases - 1)
                {
                    await done[mine + 1].Task.WaitAsync(token);
                }

                completionOrder.Enqueue(mine);
                _ = done[mine].TrySetResult();
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(
            services => services.Configure<EvaluationOptions>(static o => o.MaximumConcurrentCases = cases),
            loop);
        var plan = EvaluationTestData.Plan(
            [.. Enumerable.Range(0, cases).Select(index => Case($"c{index}"))],
            new EvaluationExecutionPolicy(cases, 1, null, null, null, false, false));

        var report = await WithTimeout(harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken));

        completionOrder.ToArray().ShouldBe([3, 2, 1, 0]);
        report.Results.Select(static r => r.CaseId.Value).ShouldBe(["c0", "c1", "c2", "c3"]);
        report.Results.Select(static r => r.CaseOrdinal).ShouldBe([0, 1, 2, 3]);
    }

    [Fact]
    public async Task RunAsync_WhenCallerCancelsMidRun_StopsSchedulingReturnsAPartialReportAndKeepsPersistedResults()
    {
        var store = new RecordingResultStore();
        var exporter = new RecordingReportExporter();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var twoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        var loop = new GatedAgentLoop
        {
            OnEntered = async (request, token) =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                {
                    _ = twoEntered.TrySetResult();
                }

                await gate.Task.WaitAsync(token);
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(
            services =>
            {
                _ = services.AddKeyedSingleton<IEvaluationResultStore>("store", store);
                _ = services.AddKeyedSingleton<IEvaluationReportExporter>("export", exporter);
            },
            loop);
        var plan = EvaluationTestData.Plan(
            [.. Enumerable.Range(0, 6).Select(index => Case($"c{index}"))],
            new EvaluationExecutionPolicy(2, 1, null, null, null, false, false),
            new EvaluationRecordingPolicy(new EvaluationResultStoreKey("store"), [new EvaluationReportExporterKey("export")]));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = harness.Runner.RunAsync(plan, cancellation.Token);
        await WithTimeout(twoEntered.Task);
        await cancellation.CancelAsync();
        var report = await WithTimeout(run);

        report.Status.ShouldBe(EvaluationReportStatus.Cancelled);
        report.Results.Length.ShouldBe(2);
        report.NotStartedCaseRuns.ShouldBe(4);
        report.Results.ShouldAllBe(static r => r.Disposition == EvaluationCaseDisposition.Cancelled);
        report.Results.ShouldAllBe(static r => r.Verdict == EvaluationVerdict.NotEvaluated);
        report.Summary.ShouldBe(new EvaluationSummary(0, 0, 0, 2, 4));
        harness.Loop.Requests.Count.ShouldBe(2);
        store.Appended.Count.ShouldBe(2);
        store.Tokens.ShouldAllBe(static token => !token.IsCancellationRequested);
        report.StoreResults.Length.ShouldBe(2);
        exporter.Reports.ShouldBeEmpty();
        var export = report.ExportResults.Single();
        export.Result.ShouldBeOfType<EvaluationExportRejected>().Kind.ShouldBe(EvaluationExportFailureKind.Cancelled);
    }

    [Fact]
    public async Task RunAsync_WhenTokenIsCancelledBeforeAnyEffect_ThrowsWithoutCreatingSessionsOrRuns()
    {
        await using var harness = await EvaluationHarness.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() => harness.Runner.RunAsync(EvaluationTestData.Plan(), cancellation.Token));

        harness.Sessions.CreateRequests.ShouldBeEmpty();
        harness.Loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenACaseExceedsItsTimeout_RecordsTimedOutAndKeepsRunningTheOthers()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var loop = new GatedAgentLoop
        {
            OnEntered = async (request, token) =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    _ = entered.TrySetResult();
                    await gate.Task.WaitAsync(token);
                }
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(loop: loop);
        var plan = EvaluationTestData.Plan(
            [Case("slow"), Case("fast")],
            new EvaluationExecutionPolicy(1, 1, TimeSpan.FromSeconds(30), null, null, false, false));

        var run = harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);
        await WithTimeout(entered.Task);
        harness.Time.Advance(TimeSpan.FromSeconds(31));
        var report = await WithTimeout(run);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.Results.Select(static r => r.Disposition).ShouldBe([EvaluationCaseDisposition.TimedOut, EvaluationCaseDisposition.Evaluated]);
        report.Results[0].Latency.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task RunAsync_WhenThePlanDeadlineElapses_ReturnsAPartialReportWithDeadlineExceeded()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loop = new GatedAgentLoop
        {
            OnEntered = async (request, token) =>
            {
                _ = entered.TrySetResult();
                await gate.Task.WaitAsync(token);
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(loop: loop);
        var plan = EvaluationTestData.Plan(
            [Case("a"), Case("b"), Case("c")],
            new EvaluationExecutionPolicy(1, 1, null, TimeSpan.FromSeconds(10), null, false, false));

        var run = harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);
        await WithTimeout(entered.Task);
        harness.Time.Advance(TimeSpan.FromSeconds(11));
        var report = await WithTimeout(run);

        report.Status.ShouldBe(EvaluationReportStatus.DeadlineExceeded);
        report.Results.Length.ShouldBe(1);
        report.NotStartedCaseRuns.ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_WhenAnExporterFails_IsolatesItAndStillPublishesToTheOthers()
    {
        var failing = new RecordingReportExporter { OnExport = static (_, _) => throw new InvalidOperationException("exporter secret") };
        var rejecting = new RecordingReportExporter
        {
            OnExport = static (_, _) => ValueTask.FromResult<EvaluationExportResult>(new EvaluationExportRejected(EvaluationExportFailureKind.Unavailable, "destination down")),
        };
        var healthy = new RecordingReportExporter();
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluationReportExporter>("failing", failing);
            _ = services.AddKeyedSingleton<IEvaluationReportExporter>("rejecting", rejecting);
            _ = services.AddKeyedSingleton<IEvaluationReportExporter>("healthy", healthy);
        });
        var plan = EvaluationTestData.Plan(
            recording: new EvaluationRecordingPolicy(
                null,
                [new EvaluationReportExporterKey("failing"), new EvaluationReportExporterKey("rejecting"), new EvaluationReportExporterKey("healthy")]));

        var report = await harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.Results.Single().Verdict.ShouldBe(EvaluationVerdict.NotEvaluated);
        report.ExportResults.Select(static e => e.Key.Value).ShouldBe(["failing", "rejecting", "healthy"]);
        report.ExportResults[0].Result.ShouldBeOfType<EvaluationExportRejected>().Kind.ShouldBe(EvaluationExportFailureKind.Faulted);
        report.ExportResults[0].Result.ShouldBeOfType<EvaluationExportRejected>().SafeMessage.ShouldNotContain("exporter secret");
        report.ExportResults[1].Result.ShouldBeOfType<EvaluationExportRejected>().Kind.ShouldBe(EvaluationExportFailureKind.Unavailable);
        _ = report.ExportResults[2].Result.ShouldBeOfType<EvaluationExported>();
        healthy.Reports.Single().ExportResults.ShouldBeEmpty();
        healthy.Reports.Single().Results.ShouldBe(report.Results);
    }

    [Fact]
    public async Task RunAsync_WhenAnExporterIsSlow_BoundsItWithTheRecordingTimeout()
    {
        var exporter = new RecordingReportExporter
        {
            OnExport = static async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return new EvaluationExported();
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluationReportExporter>("slow", exporter);
            _ = services.Configure<EvaluationOptions>(static o => o.DefaultCaseTimeout = TimeSpan.FromSeconds(5));
        });
        var plan = EvaluationTestData.Plan(recording: new EvaluationRecordingPolicy(null, [new EvaluationReportExporterKey("slow")]));

        var run = harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);
        while (exporter.Reports.Count == 0)
        {
            await Task.Yield();
        }

        harness.Time.Advance(TimeSpan.FromSeconds(6));
        var report = await WithTimeout(run);

        report.ExportResults.Single().Result.ShouldBeOfType<EvaluationExportRejected>().Kind.ShouldBe(EvaluationExportFailureKind.Cancelled);
    }

    [Fact]
    public async Task RunAsync_WhenProfileDoesNotMatchTheDefinition_FailsBeforeAnySessionRunStoreOrExportEffect()
    {
        var store = new RecordingResultStore();
        var exporter = new RecordingReportExporter();
        var key = Key();
        var evaluator = new ScriptedEvaluator(key);
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluationResultStore>("store", store);
            _ = services.AddKeyedSingleton<IEvaluationReportExporter>("export", exporter);
            _ = services.AddKeyedSingleton<IEvaluator>(key, evaluator);
        });
        var plan = EvaluationTestData.Plan(
            [
                EvaluationTestData.Case("ok", evaluators: [new EvaluatorReference(new EvaluatorKey(key))]),
                EvaluationTestData.Case("mismatch", profile: new SessionProfileKey("another-profile")),
            ],
            recording: new EvaluationRecordingPolicy(new EvaluationResultStoreKey("store"), [new EvaluationReportExporterKey("export")]));

        var exception = await Should.ThrowAsync<EvaluationPlanRejectedException>(() => harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken));

        var problem = exception.Problems.Single();
        problem.Kind.ShouldBe(EvaluationPlanProblemKind.SessionProfileMismatch);
        problem.CaseId.ShouldBe(new EvaluationCaseId("mismatch"));
        harness.Sessions.CreateRequests.ShouldBeEmpty();
        harness.Loop.Requests.ShouldBeEmpty();
        store.Appended.ShouldBeEmpty();
        exporter.Reports.ShouldBeEmpty();
        evaluator.Contexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenPlanIsIncompatibleInSeveralWays_ReportsEveryProblemInOnePass()
    {
        var supports = new ScriptedEvaluator("needs-schema", supported: [new EvaluationCriterionKey("schema")], requiresFixture: false);
        var versioned = new ScriptedEvaluator("versioned", version: 2);
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluator>("needs-schema", supports);
            _ = services.AddKeyedSingleton<IEvaluator>("versioned", versioned);
        });
        var plan = EvaluationTestData.Plan(
            [
                EvaluationTestData.Case("no-agent", agent: new AgentId(Guid.NewGuid())),
                EvaluationTestData.Case("no-evaluator", evaluators: [new EvaluatorReference(new EvaluatorKey("absent"))]),
                EvaluationTestData.Case("wrong-version", evaluators: [new EvaluatorReference(new EvaluatorKey("versioned"), new EvaluatorVersion(1))]),
                EvaluationTestData.Case("unsupported", evaluators: [new EvaluatorReference(new EvaluatorKey("needs-schema"))]),
            ],
            new EvaluationExecutionPolicy(9, 2, null, null, 3, false, false),
            new EvaluationRecordingPolicy(new EvaluationResultStoreKey("absent-store"), [new EvaluationReportExporterKey("absent-exporter")]));

        var exception = await Should.ThrowAsync<EvaluationPlanRejectedException>(() => harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken));

        exception.Problems.Select(static p => p.Kind).Order().ShouldBe(
        [
            EvaluationPlanProblemKind.ExceedsLimits,
            EvaluationPlanProblemKind.ExceedsLimits,
            EvaluationPlanProblemKind.ExceedsLimits,
            EvaluationPlanProblemKind.AgentNotFound,
            EvaluationPlanProblemKind.EvaluatorUnavailable,
            EvaluationPlanProblemKind.EvaluatorUnavailable,
            EvaluationPlanProblemKind.EvaluatorUnsupported,
            EvaluationPlanProblemKind.DestinationUnavailable,
            EvaluationPlanProblemKind.DestinationUnavailable,
        ]);
        harness.Sessions.CreateRequests.ShouldBeEmpty();
        harness.Loop.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenPlanPermitsUnsupportedEvaluators_RecordsATypedUnsupportedOutcomeWithoutInvokingTheEvaluator()
    {
        var evaluator = new ScriptedEvaluator("needs-schema", supported: [new EvaluationCriterionKey("schema")]);
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>("needs-schema", evaluator));
        var plan = EvaluationTestData.Plan(
            [EvaluationTestData.Case("c", evaluators: [new EvaluatorReference(new EvaluatorKey("needs-schema"))])],
            new EvaluationExecutionPolicy(1, 1, null, null, null, false, true));

        var report = await harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);

        _ = report.Results.Single().Evaluators.Single().Outcome.ShouldBeOfType<EvaluationUnsupported>();
        report.Results.Single().Verdict.ShouldBe(EvaluationVerdict.Inconclusive);
        evaluator.Contexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenEvaluatorThrows_RecordsItAsAFailureWithoutLeakingContentAndRunsTheNextEvaluator()
    {
        var throwing = new ScriptedEvaluator("throws", static (_, _) => throw new InvalidOperationException(_canaryReply));
        var next = new ScriptedEvaluator("next");
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluator>("throws", throwing);
            _ = services.AddKeyedSingleton<IEvaluator>("next", next);
        });

        var report = await harness.Runner.RunAsync(
            EvaluationTestData.Plan([Case("c", "throws", "next")]),
            TestContext.Current.CancellationToken);

        var result = report.Results.Single();
        var faulted = result.Evaluators[0].Outcome.ShouldBeOfType<EvaluatorFaulted>();
        faulted.ErrorType.ShouldBe("System.InvalidOperationException");
        faulted.Summary.ShouldNotContain(_canaryReply);
        _ = result.Evaluators[1].Outcome.ShouldBeOfType<EvaluationPassed>();
        result.Verdict.ShouldBe(EvaluationVerdict.Inconclusive);
        next.Contexts.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenEvaluatorReturnsNull_RecordsAnEvaluatorFailure()
    {
        var evaluator = new ScriptedEvaluator("null", static (_, _) => ValueTask.FromResult<EvaluationOutcome>(null!));
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>("null", evaluator));

        var report = await harness.Runner.RunAsync(EvaluationTestData.Plan([Case("c", "null")]), TestContext.Current.CancellationToken);

        _ = report.Results.Single().Evaluators.Single().Outcome.ShouldBeOfType<EvaluatorFaulted>();
    }

    [Fact]
    public async Task RunAsync_WhenPlanStopsOnEvaluatorFailure_StopsSchedulingLaterCases()
    {
        var evaluator = new ScriptedEvaluator("throws", static (_, _) => throw new InvalidOperationException());
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>("throws", evaluator));
        var plan = EvaluationTestData.Plan(
            [Case("a", "throws"), Case("b", "throws"), Case("c", "throws")],
            new EvaluationExecutionPolicy(1, 1, null, null, null, true, false));

        var report = await harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);

        report.Status.ShouldBe(EvaluationReportStatus.StoppedOnEvaluatorFailure);
        report.Results.Length.ShouldBe(1);
        report.NotStartedCaseRuns.ShouldBe(2);
        harness.Loop.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_WhenAnEvaluatorFailsAndThePlanDoesNotStop_RunsEveryCase()
    {
        var evaluator = new ScriptedEvaluator("throws", static (_, _) => throw new InvalidOperationException());
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>("throws", evaluator));
        var plan = EvaluationTestData.Plan([Case("a", "throws"), Case("b", "throws")]);

        var report = await harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.Results.Length.ShouldBe(2);
        report.NotStartedCaseRuns.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhenSessionCreationIsRejected_RecordsRunRejectedWithoutEvaluatingOrRunning()
    {
        var evaluator = new ScriptedEvaluator("never");
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>("never", evaluator));
        harness.Sessions.NextCreateResult = new SessionCreateFailed("store unavailable");

        var report = await harness.Runner.RunAsync(EvaluationTestData.Plan([Case("c", "never")]), TestContext.Current.CancellationToken);

        var result = report.Results.Single();
        result.Disposition.ShouldBe(EvaluationCaseDisposition.RunRejected);
        result.Run.ShouldBeNull();
        result.Evaluators.ShouldBeEmpty();
        result.Diagnostics.Single().Code.ShouldBe("session_rejected");
        harness.Loop.Requests.ShouldBeEmpty();
        evaluator.Contexts.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenTheRunEndsWithANonSuccessOutcome_RecordsItTruthfullyAndStillEvaluates()
    {
        var evaluator = new ScriptedEvaluator("judge", static (context, _) =>
            ValueTask.FromResult<EvaluationOutcome>(new EvaluationFailed(null, context.Finished!.Outcome.GetType().Name)));
        var loop = new GatedAgentLoop { OutcomeOverride = static _ => _cancelledOutcome };
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluator>("judge", evaluator), loop);

        var report = await harness.Runner.RunAsync(EvaluationTestData.Plan([Case("c", "judge")]), TestContext.Current.CancellationToken);

        var result = report.Results.Single();
        result.Run!.Outcome.ShouldBe("cancelled");
        result.Disposition.ShouldBe(EvaluationCaseDisposition.Evaluated);
        result.Verdict.ShouldBe(EvaluationVerdict.Failed);
    }

    [Fact]
    public async Task RunAsync_WhenTheEngineReportsModelUsage_RecordsTheManifestAndUsageSummary()
    {
        var loop = new GatedAgentLoop
        {
            UsageFactory = static request => new RunUsage(
                request.RunId,
                [
                    new UsageAccountingEntry(
                        new UsageEntryId(Guid.NewGuid()),
                        request.RunId,
                        new OperationId(Guid.NewGuid()),
                        new UsageAccountingRevision(1),
                        null,
                        [],
                        new ModelUsageAttribution(new ModelRequestId(Guid.NewGuid()), new ProviderId("provider"), new ApiFamilyId("family"), new ModelId("model")),
                        new ModelUsage(ModelUsageReportState.Final, 10, 12, null, null, null, null, ExtensionData.Empty),
                        ExtensionData.Empty),
                ]),
        };
        await using var harness = await EvaluationHarness.CreateAsync(loop: loop);

        var report = await harness.Runner.RunAsync(EvaluationTestData.Plan(), TestContext.Current.CancellationToken);

        var result = report.Results.Single();
        result.Usage.ShouldBe(new EvaluationUsageSummary(1, 10, 12));
        result.Manifest.ModelsUsed.ShouldBe([new EvaluationModelUse("provider", "family", "model", null)]);
    }

    [Fact]
    public async Task RunAsync_WhenAStoreIsSelected_AppendsEveryResultAndRecordsTheAcknowledgement()
    {
        var store = new RecordingResultStore();
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddKeyedSingleton<IEvaluationResultStore>("store", store));
        var plan = EvaluationTestData.Plan(
            [Case("a"), Case("b")],
            recording: new EvaluationRecordingPolicy(new EvaluationResultStoreKey("store"), []));

        var report = await harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);

        store.Appended.OrderBy(static r => r.CaseOrdinal).ToArray().ShouldBe([.. report.Results]);
        report.StoreResults.Select(static s => s.CaseId.Value).ShouldBe(["a", "b"]);
        report.StoreResults.ShouldAllBe(static s => s.Result is EvaluationStoreAppended);
    }

    [Fact]
    public async Task RunAsync_WhenTheStoreRejectsOrThrows_RecordsTheFailureWithoutChangingTheResult()
    {
        var rejecting = new RecordingResultStore
        {
            OnAppend = static (_, _) => ValueTask.FromResult<EvaluationStoreResult>(
                new EvaluationStoreRejected(new EvaluationStoreFailure(EvaluationStoreFailureKind.IdentityConflict, "conflict"))),
        };
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluationResultStore>("rejecting", rejecting);
            _ = services.AddKeyedSingleton<IEvaluationResultStore>("throwing", new ThrowingStore());
        });

        var rejected = await harness.Runner.RunAsync(
            EvaluationTestData.Plan(recording: new EvaluationRecordingPolicy(new EvaluationResultStoreKey("rejecting"), [])),
            TestContext.Current.CancellationToken);
        var thrown = await harness.Runner.RunAsync(
            EvaluationTestData.Plan(recording: new EvaluationRecordingPolicy(new EvaluationResultStoreKey("throwing"), [])),
            TestContext.Current.CancellationToken);

        rejected.StoreResults.Single().Result.ShouldBeOfType<EvaluationStoreRejected>().Failure.Kind.ShouldBe(EvaluationStoreFailureKind.IdentityConflict);
        var failure = thrown.StoreResults.Single().Result.ShouldBeOfType<EvaluationStoreRejected>().Failure;
        failure.Kind.ShouldBe(EvaluationStoreFailureKind.Unavailable);
        failure.SafeMessage.ShouldNotContain("store secret");
        rejected.Results.Single().Disposition.ShouldBe(EvaluationCaseDisposition.Evaluated);
        thrown.Results.Single().Disposition.ShouldBe(EvaluationCaseDisposition.Evaluated);
    }

    [Fact]
    public async Task RunAsync_WhenCalledTwice_GeneratesADistinctRunIdentityFromTheInjectedGenerator()
    {
        await using var harness = await EvaluationHarness.CreateAsync(
            services => services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<EvaluationRunId>>(new SequentialRunIds())));

        var first = await harness.Runner.RunAsync(EvaluationTestData.Plan(), TestContext.Current.CancellationToken);
        var second = await harness.Runner.RunAsync(EvaluationTestData.Plan(), TestContext.Current.CancellationToken);

        first.RunId.ShouldBe(new EvaluationRunId(new Guid(1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 9])));
        second.RunId.ShouldBe(new EvaluationRunId(new Guid(2, 0, 0, [0, 0, 0, 0, 0, 0, 0, 9])));
        first.Results.Single().EvaluationRunId.ShouldBe(first.RunId);
    }

    [Fact]
    public async Task RunAsync_WhenTimeIsControlled_StampsTheReportFromTheInjectedClock()
    {
        await using var harness = await EvaluationHarness.CreateAsync();
        var before = harness.Time.GetUtcNow();

        var report = await harness.Runner.RunAsync(EvaluationTestData.Plan(), TestContext.Current.CancellationToken);

        report.StartedAt.ShouldBe(before);
        report.CompletedAt.ShouldBe(before);
        report.Results.Single().StartedAt.ShouldBe(before);
        report.Results.Single().Latency.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task RunAsync_WhenListenersAreAttached_EmitsRunCaseEvaluatorAndExportSignalsCorrelatedByRunWithoutContent()
    {
        var key = Key();
        var evaluator = new ScriptedEvaluator(key);
        var exporter = new RecordingReportExporter();
        var logger = new RecordingLogger<EvaluationRunner>();
        var runIds = new SequentialRunIds();
        await using var harness = await EvaluationHarness.CreateAsync(services =>
        {
            _ = services.AddKeyedSingleton<IEvaluator>(key, evaluator);
            _ = services.AddKeyedSingleton<IEvaluationReportExporter>("export", exporter);
            _ = services.AddSingleton<ILogger<EvaluationRunner>>(logger);
            _ = services.Replace(ServiceDescriptor.Singleton<IIdentifierGenerator<EvaluationRunId>>(runIds));
        },
        new GatedAgentLoop { ReplyText = static _ => _canaryReply });
        var expectedRun = new EvaluationRunId(new Guid(1, 0, 0, [0, 0, 0, 0, 0, 0, 0, 9])).ToString();
        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName.StartsWith("evaluation.", StringComparison.Ordinal)
                    && activity.TagObjects.Any(tag => tag.Key == AgentKitTagNames.EvaluationRunId && Equals(tag.Value, expectedRun)))
                {
                    activities.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var metrics = new MetricCollector(AgentKitMetricNames.EvaluationEvaluatorCount);
        var plan = EvaluationTestData.Plan(
            [EvaluationTestData.Case("c", evaluators: [new EvaluatorReference(new EvaluatorKey(key))], input: EvaluationTestData.Input(_canaryInput))],
            recording: new EvaluationRecordingPolicy(null, [new EvaluationReportExporterKey("export")]));

        var report = await harness.Runner.RunAsync(plan, TestContext.Current.CancellationToken);

        var run = activities.Single(static a => a.OperationName == AgentKitActivityNames.EvaluationRun);
        var caseActivity = activities.Single(static a => a.OperationName == AgentKitActivityNames.EvaluationCase);
        var evaluate = activities.Single(static a => a.OperationName == AgentKitActivityNames.EvaluationEvaluate);
        var export = activities.Single(static a => a.OperationName == AgentKitActivityNames.EvaluationExport);
        run.Status.ShouldBe(ActivityStatusCode.Ok);
        caseActivity.ParentSpanId.ShouldBe(run.SpanId);
        evaluate.ParentSpanId.ShouldBe(caseActivity.SpanId);
        export.ParentSpanId.ShouldBe(run.SpanId);
        caseActivity.TraceId.ShouldBe(run.TraceId);
        report.Results.Single().TraceId.ShouldBe(run.TraceId.ToString());
        caseActivity.GetTagItem(AgentKitTagNames.EvaluationCaseId).ShouldBe("c");
        caseActivity.GetTagItem(AgentKitTagNames.EvaluationCaseRepetition).ShouldBe(1);
        caseActivity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("evaluated");
        run.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("completed");

        var entries = logger.Snapshot();
        entries.Select(static e => e.EventId.Id).Distinct().Order().ShouldBe([36000, 36001, 36003, 36004, 36008]);
        entries.Single(static e => e.EventId.Id == 36003).State["Disposition"].ShouldBe("evaluated");

        var evaluatorMetric = metrics.Snapshot().Single(m => Equals(m.Tags[AgentKitTagNames.EvaluationEvaluatorKey], key));
        evaluatorMetric.Tags[AgentKitTagNames.Outcome].ShouldBe("passed");
        SignalAssertions.ShouldNotContainContent(
            activities.Select(static a => new ActivityObservation(a.OperationName, a.Status, a.TagObjects.ToFrozenDictionary(static t => t.Key, static t => t.Value))),
            entries,
            metrics.Snapshot(),
            _canaryInput,
            _canaryReply);
    }

    [Fact]
    public async Task RunAsync_WhenTheLoggerThrows_DoesNotChangeTheReport()
    {
        var logger = new RecordingLogger<EvaluationRunner> { ThrowOnWrite = true };
        await using var harness = await EvaluationHarness.CreateAsync(services => services.AddSingleton<ILogger<EvaluationRunner>>(logger));

        var report = await harness.Runner.RunAsync(EvaluationTestData.Plan(), TestContext.Current.CancellationToken);

        report.Status.ShouldBe(EvaluationReportStatus.Completed);
        report.Results.Single().Disposition.ShouldBe(EvaluationCaseDisposition.Evaluated);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledMidRun_ReportsCancelledOutcomeInMetricsAndActivities()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loop = new GatedAgentLoop
        {
            OnEntered = async (request, token) =>
            {
                _ = entered.TrySetResult();
                await gate.Task.WaitAsync(token);
            },
        };
        await using var harness = await EvaluationHarness.CreateAsync(loop: loop);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var metrics = new MetricCollector(AgentKitMetricNames.EvaluationCaseCount);

        var run = harness.Runner.RunAsync(EvaluationTestData.Plan(), cancellation.Token);
        await WithTimeout(entered.Task);
        await cancellation.CancelAsync();
        _ = await WithTimeout(run);

        metrics.Snapshot().ShouldContain(static m => Equals(m.Tags[AgentKitTagNames.EvaluationCaseDisposition], "cancelled"));
    }
}
