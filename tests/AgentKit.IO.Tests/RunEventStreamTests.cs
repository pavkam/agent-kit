// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.Conformance;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class RunEventStreamTests: AgentRunStreamConformance
{
    [Fact]
    public async Task Completion_WhenEventConsumerOverflows_StillReturnsExactProducerResult()
    {
        await using var hub = Hub(capacity: 1);
        var producer = new TaskCompletionSource<AgentRunFinished<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var stream = hub.Subscribe(producer.Task);
        var completion = stream.Completion;
        _ = await hub.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken);
        _ = await hub.PublishAsync(RunResultTestData.Event(2), TestContext.Current.CancellationToken);
        await using var reader = stream.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        _ = await Should.ThrowAsync<RunEventSubscriptionClosedException>(() => reader.MoveNextAsync().AsTask());
        completion.IsCompleted.ShouldBeFalse();
        var result = RunResultTestData.Finished(); producer.SetResult(result);
        (await completion).ShouldBeSameAs(result);
        stream.Completion.ShouldBeSameAs(completion);
        (await stream.Completion).ShouldBeSameAs(result);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("agent")]
    [InlineData("session")]
    [InlineData("conversation")]
    [InlineData("run")]
    public async Task Completion_WhenProducerEnvelopeIsMismatched_RejectsInsteadOfPublishingAnotherRun(string mismatch)
    {
        await using var hub = Hub();
        var id = Guid.Parse("00000000-0000-0000-0000-000000000099");
        var agent = mismatch == "agent" ? new AgentId(id) : RunResultTestData.Agent;
        var session = mismatch == "session" ? new SessionId(id) : RunResultTestData.Session;
        ConversationId? conversation = mismatch == "conversation" ? new ConversationId(id) : null;
        var run = mismatch == "run" ? new RunId(id) : RunResultTestData.Run;
        var result = mismatch == "null" ? null : new AgentRunFinished<string>(agent, session, conversation, run,
            new RunSucceeded(), new RunSettlementCompleted(), "content that must not be logged",
            new(agent, session, conversation, RunResultTestData.Branch, default, default), [], new(run, []), [], ExtensionData.Empty);
        await using var stream = hub.Subscribe(Task.FromResult(result!));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => stream.Completion);
    }

    [Fact]
    public async Task Completion_WhenProducerFaultsOrCancels_PreservesOriginalFailureWithoutInventingEnvelope()
    {
        await using var hub = Hub();
        var failure = new InvalidOperationException("producer failure");
        await using var failed = hub.Subscribe(Task.FromException<AgentRunFinished<string>>(failure));
        (await Should.ThrowAsync<InvalidOperationException>(() => failed.Completion)).ShouldBeSameAs(failure);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await using var cancelled = hub.Subscribe(Task.FromCanceled<AgentRunFinished<string>>(cancellation.Token));
        (await Should.ThrowAsync<OperationCanceledException>(() => cancelled.Completion)).CancellationToken.ShouldBe(cancellation.Token);
    }


    [Theory]
    [InlineData("subscription")]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("conversationId")]
    [InlineData("runId")]
    [InlineData("completion")]
    [InlineData("observe")]
    public async Task Constructor_WhenArgumentsAreInvalid_RejectsBeforeStartingCompletionObservation(string parameter)
    {
        await using var hub = Hub(); await using var subscription = hub.Subscribe();
        var observations = 0;
        RunEventHubObservation Observe(RunEventHubOperation operation)
        {
            observations++;
            return new(operation, RunResultTestData.Agent, RunResultTestData.Session, RunResultTestData.Run, TimeProvider.System, NullLogger.Instance);
        }
        var exception = Should.Throw<ArgumentException>(() => new RunEventStream<string>(
            parameter == "subscription" ? null! : subscription,
            parameter == "agentId" ? default : RunResultTestData.Agent,
            parameter == "sessionId" ? default : RunResultTestData.Session,
            parameter == "conversationId" ? default(ConversationId) : null,
            parameter == "runId" ? default : RunResultTestData.Run,
            parameter == "completion" ? null! : Task.FromResult(RunResultTestData.Finished()), parameter == "observe" ? null! : Observe));
        exception.ParamName.ShouldBe(parameter);
        exception.GetType().ShouldBe(parameter is "subscription" or "completion" or "observe" ? typeof(ArgumentNullException) : typeof(ArgumentOutOfRangeException));
        observations.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Completion_WhenObserved_ReportsSafeCorrelatedOutcomeAndRestoresParent(bool fail, bool cancel)
    {
        using var parent = new Activity("stream-completion-parent").Start();
        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && Equals(activity.GetTagItem(AgentKitTagNames.RunEventHubOperation), "AwaitCompletion")) { captured = activity; }
            },
        };
        ActivitySource.AddActivityListener(listener);
        ConcurrentQueue<(string Name, double Value, KeyValuePair<string, object?>[] Tags)> measurements = [];
        using var meter = new MeterListener
        {
            InstrumentPublished = static (instrument, observer) =>
            {
                if (instrument.Meter.Name == AgentKitDiagnostics.MeterName
                    && instrument.Name is AgentKitMetricNames.RunEventHubOperationCount or AgentKitMetricNames.RunEventHubOperationDuration)
                { observer.EnableMeasurementEvents(instrument); }
            },
        };
        bool IsCompletion(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (Activity.Current?.TraceId != parent.TraceId) { return false; }
            foreach (var tag in tags) { if (tag.Key == AgentKitTagNames.RunEventHubOperation && Equals(tag.Value, "AwaitCompletion")) { return true; } }
            return false;
        }
        meter.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (IsCompletion(tags)) { measurements.Enqueue((instrument.Name, value, tags.ToArray())); }
        });
        meter.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (IsCompletion(tags)) { measurements.Enqueue((instrument.Name, value, tags.ToArray())); }
        });
        meter.Start();
        var logger = new CompletionLogger();
        await using var hub = Hub(logger: logger);
        var producer = new TaskCompletionSource<AgentRunFinished<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var stream = hub.Subscribe(producer.Task);
        Activity.Current.ShouldBeSameAs(parent);
        if (cancel)
        {
            producer.SetCanceled(TestContext.Current.CancellationToken);
            _ = await Should.ThrowAsync<OperationCanceledException>(() => stream.Completion);
        }
        else if (fail)
        {
            producer.SetException(new InvalidOperationException("private failure content"));
            _ = await Should.ThrowAsync<InvalidOperationException>(() => stream.Completion);
        }
        else
        {
            producer.SetResult(RunResultTestData.Finished());
            (await stream.Completion).Output.ShouldBe("output");
        }
        Activity.Current.ShouldBeSameAs(parent);
        captured.ShouldNotBeNull().Status.ShouldBe(fail ? ActivityStatusCode.Error : ActivityStatusCode.Ok);
        captured!.ParentSpanId.ShouldBe(parent.SpanId);
        captured.GetTagItem(AgentKitTagNames.RunId).ShouldBe(RunResultTestData.Run.ToString());
        var (eventId, fields) = logger.Entries.Where(static entry => Equals(entry.Fields["Operation"], "AwaitCompletion")).ShouldHaveSingleItem();
        eventId.ShouldBe(22006); fields["Outcome"].ShouldBe(cancel ? "Cancelled" : fail ? "Faulted" : "Succeeded");
        fields.Values.ShouldNotContain("output"); fields.Values.ShouldNotContain("private failure content");
        captured.TagObjects.ShouldAllBe(static tag => !Equals(tag.Value, "output") && !Equals(tag.Value, "private failure content"));
        measurements.Count.ShouldBe(2);
        measurements.ShouldContain(static item => item.Name == AgentKitMetricNames.RunEventHubOperationCount && item.Value == 1);
        foreach (var (_, _, tags) in measurements)
        {
            tags.Select(static tag => tag.Key).ShouldBe([AgentKitTagNames.RunEventHubOperation, AgentKitTagNames.Outcome]);
        }
    }

    [Fact]
    public async Task Completion_WhenObserversAndClockThrow_StillReturnsProducerEnvelope()
    {
        using var parent = new Activity("stream-completion-fault-parent").Start();
        var injected = 0;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            Sample = (ref options) => options.Parent.TraceId == parent.TraceId ? ActivitySamplingResult.AllData : ActivitySamplingResult.None,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == parent.TraceId && Equals(activity.GetTagItem(AgentKitTagNames.RunEventHubOperation), "AwaitCompletion"))
                { injected++; throw new InvalidOperationException("observer"); }
            },
        };
        ActivitySource.AddActivityListener(listener);
        await using var hub = Hub(logger: new CompletionLogger(throws: true), clock: new ThrowingClock());
        var result = RunResultTestData.Finished();
        await using var stream = hub.Subscribe(Task.FromResult(result));
        (await stream.Completion).ShouldBeSameAs(result);
        injected.ShouldBe(1); Activity.Current.ShouldBeSameAs(parent);
    }

    private static RunEventHub Hub(int capacity = 2, ILogger<RunEventHub>? logger = null, TimeProvider? clock = null) =>
        new(RunResultTestData.Agent, RunResultTestData.Session, null, RunResultTestData.Run, new(capacityPerSubscription: capacity), clock ?? TimeProvider.System, logger);

    private sealed class ThrowingClock: TimeProvider
    {
        public override long GetTimestamp() => throw new InvalidOperationException("clock");
    }

    private sealed class CompletionLogger(bool throws = false): ILogger<RunEventHub>
    {
        internal ConcurrentQueue<(int Id, Dictionary<string, object?> Fields)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (throws) { throw new InvalidOperationException("logger"); }
            Entries.Enqueue((eventId.Id, ((IEnumerable<KeyValuePair<string, object?>>) state!).ToDictionary()));
        }
    }

    protected override AgentRunStreamFixture CreateFixture() => new Fixture();

    private sealed class Fixture: AgentRunStreamFixture
    {
        private readonly RunEventHub _hub = new(RunResultTestData.Agent, RunResultTestData.Session, null, RunResultTestData.Run, new(), TimeProvider.System);
        private readonly TaskCompletionSource<AgentRunFinished<string>> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Fixture() => Stream = _hub.Subscribe(_completion.Task);
        public override IAgentRunStream<string> Stream { get; }
        public override async ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken) =>
            (await _hub.PublishAsync(runEvent, cancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        public override ValueTask CompleteAsync(AgentRunFinished<string> result, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); _hub.Complete(); _completion.SetResult(result); return ValueTask.CompletedTask;
        }
        public override async ValueTask DisposeAsync()
        {
            await Stream.DisposeAsync(); await _hub.DisposeAsync();
            _ = _completion.TrySetResult(RunResultTestData.Finished());
            _ = await Stream.Completion;
        }
    }
}
