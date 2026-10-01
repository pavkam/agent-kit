// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies bounded, per-sink draining of required run-event sinks at shutdown.</summary>
public sealed class RequiredRunEventSinkCoordinatorTests
{
    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        Should.Throw<ArgumentNullException>(() => new RequiredRunEventSinkCoordinator(null!, TimeProvider.System)).ParamName.ShouldBe("sinks");
        Should.Throw<ArgumentNullException>(() => new RequiredRunEventSinkCoordinator([], null!)).ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public async Task DrainAsync_WhenNoRequiredSinkIsRegistered_ReturnsEmptyWithoutWaiting()
    {
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("best-effort", RunEventDelivery.BestEffort, new BlockingFlushableSink())], new FakeTimeProvider());

        var result = await coordinator.DrainAsync(TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(RequiredRunEventSinkDrainResult.Empty);
    }

    [Fact]
    public async Task DrainAsync_WhenARequiredSinkHasNothingBuffered_CountsItAsDrainedImmediately()
    {
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("inline", RunEventDelivery.Required, new FakeRunEventSink())], new FakeTimeProvider());

        var result = await coordinator.DrainAsync(TestContext.Current.CancellationToken);

        result.Drained.ShouldBe(["inline"]);
        result.IsClean.ShouldBeTrue();
    }

    [Fact]
    public async Task DrainAsync_WhenAFlushableSinkCompletesBeforeItsDeadline_IsDrained()
    {
        var sink = new BlockingFlushableSink();
        var coordinator = new RequiredRunEventSinkCoordinator([Binding("queue", RunEventDelivery.Required, sink)], new FakeTimeProvider());

        var drain = coordinator.DrainAsync(TestContext.Current.CancellationToken).AsTask();
        await sink.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        drain.IsCompleted.ShouldBeFalse();
        sink.Release();
        var result = await drain;

        result.Drained.ShouldBe(["queue"]);
        sink.FlushCount.ShouldBe(1);
    }

    [Fact]
    public async Task DrainAsync_WhenAFlushableSinkMissesItsDeadline_ReportsTimedOutWithoutThrowing()
    {
        var clock = new FakeTimeProvider();
        var sink = new BlockingFlushableSink(honourCancellation: false);
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("stuck", RunEventDelivery.Required, sink, TimeSpan.FromSeconds(5))], clock);

        var drain = coordinator.DrainAsync(TestContext.Current.CancellationToken).AsTask();
        await sink.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(5));
        var result = await drain.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.TimedOut.ShouldBe(["stuck"]);
        result.IsClean.ShouldBeFalse();
        sink.Release();
    }

    [Fact]
    public async Task DrainAsync_WhenAFlushFaults_ReportsFailedAndStillDrainsTheOtherSinks()
    {
        var coordinator = new RequiredRunEventSinkCoordinator(
            [
                Binding("broken", RunEventDelivery.Required, new FaultingFlushableSink(), order: 0),
                Binding("healthy", RunEventDelivery.Required, new BlockingFlushableSink(release: true), order: 1),
            ],
            new FakeTimeProvider());

        var result = await coordinator.DrainAsync(TestContext.Current.CancellationToken);

        result.Failed.ShouldBe(["broken"]);
        result.Drained.ShouldBe(["healthy"]);
    }

    [Fact]
    public async Task DrainAsync_WhenAFlushThrowsSynchronously_ReportsFailed()
    {
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("sync", RunEventDelivery.Required, new SynchronousFaultingFlushableSink())], new FakeTimeProvider());

        var result = await coordinator.DrainAsync(TestContext.Current.CancellationToken);

        result.Failed.ShouldBe(["sync"]);
    }

    [Fact]
    public async Task DrainAsync_WhenSeveralSinksBuffer_StartsEveryFlushBeforeAwaitingAny()
    {
        var first = new BlockingFlushableSink();
        var second = new BlockingFlushableSink();
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("first", RunEventDelivery.Required, first, order: 0), Binding("second", RunEventDelivery.Required, second, order: 1)],
            new FakeTimeProvider());

        var drain = coordinator.DrainAsync(TestContext.Current.CancellationToken).AsTask();
        await first.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await second.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        first.Release();
        second.Release();
        var result = await drain;

        result.Drained.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task DrainAsync_WhenSinksDeclareDifferentDeadlines_AppliesEachSinksOwnDeadline()
    {
        var clock = new FakeTimeProvider();
        var quick = new BlockingFlushableSink(honourCancellation: false);
        var patient = new BlockingFlushableSink();
        var coordinator = new RequiredRunEventSinkCoordinator(
            [
                Binding("quick", RunEventDelivery.Required, quick, TimeSpan.FromSeconds(1), order: 0),
                Binding("patient", RunEventDelivery.Required, patient, TimeSpan.FromSeconds(60), order: 1),
            ],
            clock);

        var drain = coordinator.DrainAsync(TestContext.Current.CancellationToken).AsTask();
        await quick.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await patient.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(2));
        patient.Release();
        var result = await drain.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        result.TimedOut.ShouldBe(["quick"]);
        result.Drained.ShouldBe(["patient"]);
        quick.Release();
    }

    [Fact]
    public async Task DrainAsync_WhenTheCallerCancels_ThrowsOperationCanceledException()
    {
        var sink = new BlockingFlushableSink();
        var coordinator = new RequiredRunEventSinkCoordinator([Binding("queue", RunEventDelivery.Required, sink)], new FakeTimeProvider());
        using var cts = new CancellationTokenSource();

        var drain = coordinator.DrainAsync(cts.Token).AsTask();
        await sink.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await drain.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        sink.Release();
    }

    [Fact]
    public async Task DrainAsync_WhenTheTokenIsAlreadyCancelled_ThrowsBeforeFlushing()
    {
        var sink = new BlockingFlushableSink();
        var coordinator = new RequiredRunEventSinkCoordinator([Binding("queue", RunEventDelivery.Required, sink)], new FakeTimeProvider());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await coordinator.DrainAsync(cts.Token));

        sink.FlushCount.ShouldBe(0);
    }

    [Fact]
    public async Task DrainAsync_WhenTheLoggerThrows_StillReportsTheOutcome()
    {
        var logger = new RecordingLogger<RequiredRunEventSinkCoordinator> { ThrowOnWrite = true };
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("inline", RunEventDelivery.Required, new FakeRunEventSink())], new FakeTimeProvider(), logger);

        var result = await coordinator.DrainAsync(TestContext.Current.CancellationToken);

        result.Drained.ShouldBe(["inline"]);
    }

    [Fact]
    public async Task DrainAsync_WhenASinkTimesOut_LogsOnlyTheSinkNameAndDeadline()
    {
        var clock = new FakeTimeProvider();
        var logger = new RecordingLogger<RequiredRunEventSinkCoordinator>();
        var sink = new BlockingFlushableSink(honourCancellation: false);
        var coordinator = new RequiredRunEventSinkCoordinator(
            [Binding("stuck", RunEventDelivery.Required, sink, TimeSpan.FromSeconds(3))], clock, logger);

        var drain = coordinator.DrainAsync(TestContext.Current.CancellationToken).AsTask();
        await sink.FlushStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        _ = await drain.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(22014);
        entry.Level.ShouldBe(LogLevel.Warning);
        sink.Release();
    }

    private static RunEventSinkBinding Binding(
        string name,
        RunEventDelivery delivery,
        IRunEventSink sink,
        TimeSpan flushDeadline = default,
        int order = 0) =>
        new(new RunEventSinkRegistration(name, delivery, order, flushDeadline), sink);

    private sealed class BlockingFlushableSink(bool honourCancellation = true, bool release = false): IFlushableRunEventSink
    {
        private readonly TaskCompletionSource _gate = CreateGate(release);
        private int _flushCount;

        public TaskCompletionSource FlushStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int FlushCount => Volatile.Read(ref _flushCount);

        public void Release() => _ = _gate.TrySetResult();

        public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _flushCount);
            _ = FlushStarted.TrySetResult();
            return new ValueTask(honourCancellation ? _gate.Task.WaitAsync(cancellationToken) : _gate.Task);
        }

        private static TaskCompletionSource CreateGate(bool release)
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (release)
            {
                gate.SetResult();
            }

            return gate;
        }
    }

    private sealed class FaultingFlushableSink: IFlushableRunEventSink
    {
        public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("flush failed"));
    }

    private sealed class SynchronousFaultingFlushableSink: IFlushableRunEventSink
    {
        public ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("flush failed");
    }
}
