// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

public sealed class RunEventHubTests
{
    private static readonly AgentId Agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId Session = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static readonly RunId Run = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static readonly TurnId Turn = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static readonly MessageId Message = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));


    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("conversationId")]
    [InlineData("runId")]
    public void Constructor_WhenIdentityIsDefault_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventHub(
            parameter == "agentId" ? default : Agent,
            parameter == "sessionId" ? default : Session,
            parameter == "conversationId" ? default(ConversationId) : null,
            parameter == "runId" ? default : Run, new RunEventHubOptions(), TimeProvider.System));
        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenOptionsAreNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new RunEventHub(Agent, Session, null, Run, null!, TimeProvider.System)).ParamName.ShouldBe("options");


    [Fact]
    public async Task Subscribe_WhenPublicationAlreadyHappened_ReceivesOnlyLaterRecipientSnapshot()
    {
        await using var hub = Hub();
        _ = await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken);
        await using var subscription = hub.Subscribe();
        var second = Event(2);
        _ = await hub.PublishAsync(second, TestContext.Current.CancellationToken);
        hub.Complete();

        (await ReadAsync(subscription)).ShouldBe([second]);
    }

    [Fact]
    public async Task Subscribe_WhenCapacityIsReached_RejectsUntilExistingSubscriberReleasesRegistration()
    {
        await using var hub = Hub(subscribers: 1);
        var first = hub.Subscribe();
        Should.Throw<RunEventSubscriptionRejectedException>(hub.Subscribe).Reason.ShouldBe(RunEventSubscriptionRejection.CapacityReached);
        await first.DisposeAsync();
        await first.DisposeAsync();

        await using var replacement = hub.Subscribe();
        replacement.State.ShouldBe(RunEventSubscriptionState.Active);
    }

    [Fact]
    public async Task PublishAsync_WhenSlowConsumerFillsBuffer_DisconnectsExplicitlyAndPreservesHealthyConsumer()
    {
        await using var hub = Hub(capacity: 1);
        await using var slow = hub.Subscribe();
        await using var healthy = hub.Subscribe();
        await using var reader = healthy.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var pending = reader.MoveNextAsync().AsTask();
        _ = await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken);
        (await pending).ShouldBeTrue();
        reader.Current.Sequence.ShouldBe(1);
        pending = reader.MoveNextAsync().AsTask();

        (await hub.PublishAsync(Event(2), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);

        (await pending).ShouldBeTrue();
        reader.Current.Sequence.ShouldBe(2);
        slow.State.ShouldBe(RunEventSubscriptionState.SlowConsumer);
        var failure = await Should.ThrowAsync<RunEventSubscriptionClosedException>(() => ReadAsync(slow));
        failure.State.ShouldBe(RunEventSubscriptionState.SlowConsumer);
        failure.FirstUnavailableSequence.ShouldBe(1);
        await using var replacement = hub.Subscribe();
    }





    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task End_WhenReaderIsWaiting_WakesWithNormalEndOrExplicitHubFailure(bool complete)
    {
        var hub = Hub();
        await using var subscription = hub.Subscribe();
        await using var reader = subscription.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var pending = reader.MoveNextAsync().AsTask();
        pending.IsCompleted.ShouldBeFalse();
        if (complete) { hub.Complete(); }
        else { await hub.DisposeAsync(); }
        if (complete) { (await pending).ShouldBeFalse(); }
        else
        {
            var failure = await Should.ThrowAsync<RunEventSubscriptionClosedException>(() => pending);
            failure.State.ShouldBe(RunEventSubscriptionState.HubDisposed);
            failure.FirstUnavailableSequence.ShouldBeNull();
        }
        await hub.DisposeAsync();
        Should.Throw<RunEventSubscriptionRejectedException>(hub.Subscribe).Reason.ShouldBe(RunEventSubscriptionRejection.HubClosed);
        (await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.HubClosed);
    }

    [Fact]
    public async Task DisposeAsync_WhenEventsRemain_ReportsFirstUnavailableDurableSequence()
    {
        var hub = Hub();
        await using var subscription = hub.Subscribe();
        _ = await hub.PublishAsync(Event(7), TestContext.Current.CancellationToken);
        await hub.DisposeAsync();
        var failure = await Should.ThrowAsync<RunEventSubscriptionClosedException>(() => ReadAsync(subscription));
        failure.FirstUnavailableSequence.ShouldBe(7);
    }

    [Fact]
    public async Task PublishAsync_WhenSequenceDoesNotAdvance_RejectsWithoutDuplicatingEvents()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        _ = await hub.PublishAsync(Event(3), TestContext.Current.CancellationToken);
        (await hub.PublishAsync(Event(3), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.OutOfOrder);
        (await hub.PublishAsync(Event(2), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.OutOfOrder);
        (await hub.PublishAsync(Event(4), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        hub.Complete();
        (await ReadAsync(subscription)).Select(static item => item.Sequence).ShouldBe([3L, 4L]);
    }

    [Fact]
    public async Task PublishAsync_WhenConcurrentCallsUseSameSequence_AcceptsExactlyOne()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
            Task.Run(async () => await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        outcomes.Count(static outcome => outcome == RunEventPublicationOutcome.Published).ShouldBe(1);
        hub.Complete();
        (await ReadAsync(subscription)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_WhenCancelled_DoesNotConsumeSequenceOrDeliver()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(() => hub.PublishAsync(Event(1), cancellation.Token).AsTask());
        (await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        hub.Complete();
        (await ReadAsync(subscription)).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("session")]
    [InlineData("conversation")]
    [InlineData("run")]
    public async Task PublishAsync_WhenCorrelationDiffers_RejectsBeforeQueueOrSequenceMutation(string mismatch)
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        var invalid = new MessageCommittedEvent(
            mismatch == "agent" ? new AgentId(Guid.NewGuid()) : Agent,
            mismatch == "session" ? new SessionId(Guid.NewGuid()) : Session,
            mismatch == "conversation" ? new ConversationId(Guid.NewGuid()) : null,
            mismatch == "run" ? new RunId(Guid.NewGuid()) : Run, Turn, 1, DateTimeOffset.UnixEpoch, Message, new SessionVersion(1));
        (await Should.ThrowAsync<ArgumentException>(() => hub.PublishAsync(invalid, TestContext.Current.CancellationToken).AsTask())).ParamName.ShouldBe("runEvent");
        (await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
    }

    [Fact]
    public async Task PublishAsync_WhenEventIsNull_RejectsExactArgument()
    {
        await using var hub = Hub();
        (await Should.ThrowAsync<ArgumentNullException>(() => hub.PublishAsync(null!, TestContext.Current.CancellationToken).AsTask())).ParamName.ShouldBe("runEvent");
    }

    [Fact]
    public void Constructor_WhenClockIsNull_RejectsBeforeObservation() =>
        Should.Throw<ArgumentNullException>(() => new RunEventHub(Agent, Session, null, Run, new RunEventHubOptions(), null!)).ParamName.ShouldBe("timeProvider");







    internal static RunEventHub Hub(int subscribers = 2, int capacity = 2) => new(Agent, Session, null, Run, new RunEventHubOptions(subscribers, capacity), TimeProvider.System);
    internal static MessageCommittedEvent Event(long sequence) => new(Agent, Session, null, Run, Turn, sequence, DateTimeOffset.UnixEpoch, Message, new SessionVersion(1));

    internal static async Task<List<RunEvent>> ReadAsync(RunEventSubscription subscription)
    {
        List<RunEvent> events = [];
        await foreach (var item in subscription.ReadAllAsync(TestContext.Current.CancellationToken)) { events.Add(item); }
        return events;
    }

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
        var basis = Event(1);
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

        (await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
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

        (await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);

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
        _ = await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken);
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
            _ = await Should.ThrowAsync<OperationCanceledException>(() => hub.PublishAsync(Event(2), cancellation.Token).AsTask());
        }
        else { _ = await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken); }

        stopped.ShouldNotBeNull().Status.ShouldBe(ActivityStatusCode.Error);
        stopped!.GetTagItem(AgentKitTagNames.Outcome).ShouldBe(expected);
        logger.Entries.ShouldHaveSingleItem().Fields["Outcome"].ShouldBe(expected);
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
        var basis = Event(1);
        return new RunEventHub(basis.AgentId, basis.SessionId, null, basis.RunId, new RunEventHubOptions(), clock, logger);
    }

    internal sealed class FixedClock(bool throws = false): TimeProvider
    {
        public override long GetTimestamp() => throws ? throw new InvalidOperationException("clock") : 0;
    }

    internal sealed class HubLogger(bool throws = false): ILogger<RunEventHub>
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

    [Fact]
    public async Task Subscribe_WhenCompletionTaskIsNull_RejectsBeforeAllocatingSubscriptionCapacity()
    {
        await using var hub = new RunEventHub(RunResultTestData.Agent, RunResultTestData.Session, null, RunResultTestData.Run, new(1), TimeProvider.System);
        Should.Throw<ArgumentNullException>(() => hub.Subscribe<string>(null!)).ParamName.ShouldBe("completion");
        await using var available = hub.Subscribe(Task.FromResult(RunResultTestData.Finished()));
        (await available.Completion).IsCleanSuccess.ShouldBeTrue();
    }
}
