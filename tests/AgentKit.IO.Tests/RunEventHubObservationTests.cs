// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventHubObservationTests
{
    [Fact]
    public async Task PublishAsync_WhenObserved_EmitsSafeParentedTraceLogAndBoundedMetrics()
    {
        using var parent = new Activity("hub-observation-parent").Start();
        ConcurrentQueue<Activity> activities = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId
                    && (activity.GetTagItem(AgentKitTagNames.RunEventHubOperation) as string) == "Publish") { activities.Enqueue(activity); }
            },
        };
        ActivitySource.AddActivityListener(listener);
        ConcurrentQueue<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using var meter = CreateMeter();
        meter.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (IsPublication(parent.TraceId, tags)) { measurements.Enqueue((instrument.Name, value, tags.ToArray())); }
        });
        meter.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (IsPublication(parent.TraceId, tags)) { measurements.Enqueue((instrument.Name, value, tags.ToArray())); }
        });
        meter.Start();
        var logger = new HubLogger();
        var basis = RunEventHubTests.Event(1);
        const string content = "seeded private prompt and tool payload";
        var delta = new ContentDeltaEvent(basis.AgentId, basis.SessionId, null, basis.RunId, basis.TurnId,
            1, DateTimeOffset.UnixEpoch, new ModelRequestId(Guid.NewGuid()), 0, new TextContentDelta(content));
        await using var hub = CreateHub(logger, new FixedClock());

        (await hub.PublishAsync(delta, TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);

        Activity.Current.ShouldBeSameAs(parent);
        var activity = activities.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AgentKitActivityNames.RunEventHub);
        activity.ParentSpanId.ShouldBe(parent.SpanId);
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("Succeeded");
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(basis.RunId.ToString());
        activity.TagObjects.ShouldAllBe(tag => !Equals(tag.Value, content));
        var (logId, logLevel, logFields) = logger.Entries.ShouldHaveSingleItem();
        logId.ShouldBe(1006);
        logLevel.ShouldBe(LogLevel.Debug);
        logFields["Operation"].ShouldBe("Publish");
        logFields["RunId"].ShouldBe(basis.RunId);
        logFields.Values.ShouldNotContain(content);
        measurements.Count.ShouldBe(2);
        measurements.ShouldContain(item => item.Name == AgentKitMetricNames.RunEventHubOperationCount && item.Value == 1);
        measurements.ShouldContain(item => item.Name == AgentKitMetricNames.RunEventHubOperationDuration && item.Value == 0);
        foreach (var (_, _, tags) in measurements)
        {
            tags.Select(static tag => tag.Key).ShouldBe([AgentKitTagNames.RunEventHubOperation, AgentKitTagNames.Outcome]);
            tags.ShouldAllBe(static tag => tag.Value is string);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAsync_WhenActivityLoggerAndMetricsThrow_PreservesDeliveryAndParentage(bool throwOnStart)
    {
        using var parent = new Activity("hub-fault-parent").Start();
        var failures = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStarted = activity =>
            {
                if (throwOnStart && IsPublication(activity, parent.TraceId)) { failures++; throw new InvalidOperationException("observer"); }
            },
            ActivityStopped = activity =>
            {
                if (!throwOnStart && IsPublication(activity, parent.TraceId)) { failures++; throw new InvalidOperationException("observer"); }
            },
        };
        ActivitySource.AddActivityListener(listener);
        using var meter = CreateMeter();
        meter.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (IsPublication(parent.TraceId, tags)) { throw new InvalidOperationException("metric"); }
        });
        meter.Start();
        await using var hub = CreateHub(new HubLogger(throws: true), new FixedClock());
        await using var subscription = hub.Subscribe();

        (await hub.PublishAsync(RunEventHubTests.Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        hub.Complete();

        Activity.Current.ShouldBeSameAs(parent);
        failures.ShouldBe(1);
        var count = 0;
        await foreach (var item in subscription.ReadAllAsync(TestContext.Current.CancellationToken)) { count++; item.Sequence.ShouldBe(1); }
        count.ShouldBe(1);
        Activity.Current.ShouldBeSameAs(parent);
    }

    [Fact]
    public async Task PublishAsync_WhenClockThrows_PreservesCountAndOmitsDuration()
    {
        using var parent = new Activity("hub-clock-parent").Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
        };
        ActivitySource.AddActivityListener(listener);
        ConcurrentQueue<string> measurements = [];
        using var meter = CreateMeter();
        meter.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (IsPublication(parent.TraceId, tags)) { measurements.Enqueue(instrument.Name); }
        });
        meter.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (IsPublication(parent.TraceId, tags)) { measurements.Enqueue(instrument.Name); }
        });
        meter.Start();
        await using var hub = CreateHub(new HubLogger(), new FixedClock(throws: true));

        (await hub.PublishAsync(RunEventHubTests.Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);

        measurements.ShouldBe([AgentKitMetricNames.RunEventHubOperationCount]);
    }

    [Theory]
    [InlineData("out-of-order", "OutOfOrder")]
    [InlineData("closed", "HubClosed")]
    [InlineData("cancelled", "Cancelled")]
    public async Task PublishAsync_WhenRejectedOrCancelled_ReportsTruthfulErrorOutcome(string scenario, string expected)
    {
        using var parent = new Activity("hub-rejection-parent").Start();
        var logger = new HubLogger();
        await using var hub = CreateHub(logger, new FixedClock());
        _ = await hub.PublishAsync(RunEventHubTests.Event(1), TestContext.Current.CancellationToken);
        if (scenario == "closed") { hub.Complete(); }
        logger.Entries.Clear();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity => { if (IsPublication(activity, parent.TraceId)) { stopped = activity; } },
        };
        ActivitySource.AddActivityListener(listener);
        using var cancellation = new CancellationTokenSource();
        if (scenario == "cancelled")
        {
            cancellation.Cancel();
            _ = await Should.ThrowAsync<OperationCanceledException>(() => hub.PublishAsync(RunEventHubTests.Event(2), cancellation.Token).AsTask());
        }
        else { _ = await hub.PublishAsync(RunEventHubTests.Event(1), TestContext.Current.CancellationToken); }

        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Error);
        stopped!.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(expected);
        logger.Entries.ShouldHaveSingleItem().Fields["Outcome"].ShouldBe(expected);
    }

    [Theory]
    [InlineData("completed", "Succeeded", ActivityStatusCode.Ok)]
    [InlineData("disposed", "Abandoned", ActivityStatusCode.Ok)]
    [InlineData("abandoned", "Abandoned", ActivityStatusCode.Ok)]
    [InlineData("cancelled", "Cancelled", ActivityStatusCode.Error)]
    [InlineData("slow", "SlowConsumer", ActivityStatusCode.Error)]
    [InlineData("hub-disposed", "HubDisposed", ActivityStatusCode.Error)]
    public async Task ReadAllAsync_WhenDeliveryEnds_ObservesTruthfulOutcomeAndReleasesWaitingReader(string scenario, string expected, ActivityStatusCode status)
    {
        using var parent = new Activity("hub-read-parent").Start();
        Activity? stopped = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && Equals(activity.GetTagItem(AgentKitTagNames.RunEventHubOperation), "Read")) { stopped = activity; }
            },
        };
        ActivitySource.AddActivityListener(listener);
        var logger = new HubLogger();
        var basis = RunEventHubTests.Event(1);
        await using var hub = new RunEventHub(basis.AgentId, basis.SessionId, null, basis.RunId,
            new RunEventHubOptions(maximumSubscriptions: 1, capacityPerSubscription: 1), new FixedClock(), logger);
        await using var subscription = hub.Subscribe();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var reader = subscription.ReadAllAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var pending = reader.MoveNextAsync().AsTask();
        pending.IsCompleted.ShouldBeFalse();

        switch (scenario)
        {
            case "completed":
                hub.Complete();
                (await pending).ShouldBeFalse();
                break;
            case "disposed":
                await subscription.DisposeAsync();
                (await pending).ShouldBeFalse();
                break;
            case "cancelled":
                cancellation.Cancel();
                _ = await Should.ThrowAsync<OperationCanceledException>(() => pending);
                break;
            case "hub-disposed":
                await hub.DisposeAsync();
                _ = await Should.ThrowAsync<RunEventSubscriptionClosedException>(() => pending);
                break;
            default:
                _ = await hub.PublishAsync(basis, TestContext.Current.CancellationToken);
                (await pending).ShouldBeTrue();
                if (scenario == "slow")
                {
                    _ = await hub.PublishAsync(RunEventHubTests.Event(2), TestContext.Current.CancellationToken);
                    _ = await hub.PublishAsync(RunEventHubTests.Event(3), TestContext.Current.CancellationToken);
                    _ = await Should.ThrowAsync<RunEventSubscriptionClosedException>(() => reader.MoveNextAsync().AsTask());
                }
                else { await reader.DisposeAsync(); }
                break;
        }

        stopped.ShouldNotBeNull().Status.ShouldBe(status);
        stopped!.ParentSpanId.ShouldBe(parent.SpanId);
        stopped.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(expected);
        logger.Entries.Where(static entry => Equals(entry.Fields["Operation"], "Read"))
            .ShouldHaveSingleItem().Fields["Outcome"].ShouldBe(expected);
        Activity.Current.ShouldBeSameAs(parent);
        if (scenario is not ("completed" or "hub-disposed"))
        {
            await using var replacement = hub.Subscribe();
            (await hub.PublishAsync(RunEventHubTests.Event(4), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        }
    }

    private static bool IsPublication(Activity activity, ActivityTraceId traceId) =>
        activity.TraceId == traceId && (activity.GetTagItem(AgentKitTagNames.RunEventHubOperation) as string) == "Publish";

    private static bool IsPublication(ActivityTraceId traceId, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (Activity.Current?.TraceId != traceId) { return false; }
        foreach (var tag in tags) { if (tag.Key == AgentKitTagNames.RunEventHubOperation && Equals(tag.Value, "Publish")) { return true; } }
        return false;
    }

    private static MeterListener CreateMeter() => new()
    {
        InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                && instrument.Name is AgentKitMetricNames.RunEventHubOperationCount or AgentKitMetricNames.RunEventHubOperationDuration)
            { listener.EnableMeasurementEvents(instrument); }
        },
    };

    private static RunEventHub CreateHub(ILogger<RunEventHub> logger, TimeProvider clock)
    {
        var basis = RunEventHubTests.Event(1);
        return new RunEventHub(basis.AgentId, basis.SessionId, null, basis.RunId, new RunEventHubOptions(), clock, logger);
    }

    private sealed class FixedClock(bool throws = false): TimeProvider
    {
        public override long GetTimestamp() => throws ? throw new InvalidOperationException("clock") : 0;
    }

    private sealed class HubLogger(bool throws = false): ILogger<RunEventHub>
    {
        internal ConcurrentQueue<(int Id, LogLevel Level, Dictionary<string, object?> Fields)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (throws) { throw new InvalidOperationException("logger"); }
            Entries.Enqueue((eventId.Id, logLevel, ((IEnumerable<KeyValuePair<string, object?>>) state!).ToDictionary()));
        }
    }
}
