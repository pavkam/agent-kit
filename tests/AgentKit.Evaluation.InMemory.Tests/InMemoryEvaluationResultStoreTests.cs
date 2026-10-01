// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

public sealed class InMemoryEvaluationResultStoreTests
{
    [Fact]
    public void Constructor_WhenTimeIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new InMemoryEvaluationResultStore(null!)).ParamName.ShouldBe("time");

    [Fact]
    public async Task AppendAsync_WhenAnotherStoreInstanceIsCreated_StartsEmptyBecauseNothingIsDurable()
    {
        var run = EvaluationResultConformanceData.NewRun();
        var first = new InMemoryEvaluationResultStore(TimeProvider.System);
        _ = await first.AppendAsync(EvaluationResultConformanceData.Result(run), TestContext.Current.CancellationToken);

        var second = new InMemoryEvaluationResultStore(TimeProvider.System);

        (await second.ReadAsync(new EvaluationResultQuery(run, 5), TestContext.Current.CancellationToken))
            .ShouldBeOfType<EvaluationResultsRead>().Results.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppendAsync_WhenTheLoggerThrows_DoesNotChangeTheOutcome()
    {
        var logger = new RecordingLogger<InMemoryEvaluationResultStore> { ThrowOnWrite = true };
        var store = new InMemoryEvaluationResultStore(TimeProvider.System, logger);

        var answer = await store.AppendAsync(EvaluationResultConformanceData.Result(EvaluationResultConformanceData.NewRun()), TestContext.Current.CancellationToken);

        answer.ShouldBeOfType<EvaluationStoreAppended>().Replayed.ShouldBeFalse();
    }

    [Fact]
    public async Task AppendAsync_WhenObserved_EmitsAnActivityLogAndMetricWithBoundedFieldsAndNoContent()
    {
        var run = EvaluationResultConformanceData.NewRun();
        var logger = new RecordingLogger<InMemoryEvaluationResultStore>();
        var store = new InMemoryEvaluationResultStore(TimeProvider.System, logger);
        var stopped = new System.Collections.Concurrent.ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.OperationName == AgentKitActivityNames.EvaluationStoreOperation && Equals(activity.GetTagItem(AgentKitTagNames.EvaluationRunId), run.ToString()))
                {
                    stopped.Enqueue(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var metrics = new MetricCollector(AgentKitMetricNames.EvaluationStoreOperationCount);

        _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run), TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(EvaluationResultConformanceData.Result(run, latencyMilliseconds: 1), TestContext.Current.CancellationToken);
        _ = await store.ReadAsync(new EvaluationResultQuery(run, 5), TestContext.Current.CancellationToken);

        var operations = stopped.Select(static a => (a.GetTagItem(AgentKitTagNames.EvaluationStoreOperation), a.GetTagItem(AgentKitTagNames.Outcome), a.Status)).ToArray();
        operations.ShouldBe(
        [
            ("append", "completed", ActivityStatusCode.Ok),
            ("append", "identity_conflict", ActivityStatusCode.Error),
            ("read", "completed", ActivityStatusCode.Ok),
        ]);
        stopped.ShouldAllBe(static a => Equals(a.GetTagItem(AgentKitTagNames.EvaluationStoreAdapter), "in_memory"));
        logger.Snapshot().Select(static e => e.EventId.Id).ShouldBe([36100, 36100, 36100]);
        metrics.Snapshot().ShouldContain(static m => Equals(m.Tags[AgentKitTagNames.EvaluationStoreAdapter], "in_memory") && Equals(m.Tags[AgentKitTagNames.Outcome], "identity_conflict"));
        SignalAssertions.ShouldNotContainContent(
            stopped.Select(static a => new ActivityObservation(a.OperationName, a.Status, a.TagObjects.ToDictionary(static t => t.Key, static t => t.Value).ToFrozenDictionary())),
            logger.Snapshot(),
            metrics.Snapshot(),
            "safe note",
            "0af7651916cd43dd8448eb211c80319c");
    }

    [Fact]
    public async Task AppendAsync_WhenCancelled_RecordsACancelledOutcomeAndRethrows()
    {
        var logger = new RecordingLogger<InMemoryEvaluationResultStore>();
        var store = new InMemoryEvaluationResultStore(TimeProvider.System, logger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await store.AppendAsync(EvaluationResultConformanceData.Result(EvaluationResultConformanceData.NewRun()), cancellation.Token));

        logger.Snapshot().Single().EventId.Id.ShouldBe(36101);
    }
}
