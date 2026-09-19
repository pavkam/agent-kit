// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Verifies DefaultOutputPublisher behavior and contracts.</summary>
public sealed class DefaultOutputPublisherTests
{
    [Fact]
    public void Constructor_WhenSinksAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new DefaultOutputPublisher(
            null!, new FakeOutputBackpressurePolicy(BackpressureDecision.Wait), Hub(), TimeProvider.System, NullLogger<DefaultOutputPublisher>.Instance))
            .ParamName.ShouldBe("sinks");

    [Fact]
    public void Constructor_WhenBackpressurePolicyIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new DefaultOutputPublisher(
            [], null!, Hub(), TimeProvider.System, NullLogger<DefaultOutputPublisher>.Instance))
            .ParamName.ShouldBe("backpressurePolicy");

    [Fact]
    public void Constructor_WhenEventHubIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new DefaultOutputPublisher(
            [], new FakeOutputBackpressurePolicy(BackpressureDecision.Wait), null!, TimeProvider.System, NullLogger<DefaultOutputPublisher>.Instance))
            .ParamName.ShouldBe("eventHub");

    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new DefaultOutputPublisher(
            [], new FakeOutputBackpressurePolicy(BackpressureDecision.Wait), Hub(), null!, NullLogger<DefaultOutputPublisher>.Instance))
            .ParamName.ShouldBe("timeProvider");

    [Fact]
    public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new DefaultOutputPublisher(
            [], new FakeOutputBackpressurePolicy(BackpressureDecision.Wait), Hub(), TimeProvider.System, null!))
            .ParamName.ShouldBe("logger");

    [Fact]
    public async Task PublishAsync_WhenEventIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        await using var hub = Hub();
        var publisher = Publisher(eventHub: hub);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => publisher.PublishAsync(null!, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("runEvent");
    }

    [Fact]
    public async Task PublishAsync_ForwardsTheEventToTheHub()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        var publisher = Publisher(eventHub: hub);

        await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken);
        hub.Complete();

        (await RunEventHubTests.ReadAsync(subscription)).ShouldBe([RunResultTestData.Event(1)]);
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkFaults_PropagatesTheFaultAndStillDeliversTheHubEvent()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        var sink = new FakeRunEventSink((_, _) => throw new InvalidOperationException("simulated"));
        var publisher = Publisher([Required(sink)], eventHub: hub);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken).AsTask());
        hub.Complete();

        _ = sink.Received.ShouldHaveSingleItem();
        (await RunEventHubTests.ReadAsync(subscription)).ShouldBe([RunResultTestData.Event(1)]);
    }

    [Fact]
    public async Task PublishAsync_WhenABestEffortSinkFaults_IsolatesTheFaultAndCompletesNormally()
    {
        await using var hub = Hub();
        var faulted = new FakeRunEventSink((_, _) => throw new InvalidOperationException("simulated"));
        var succeeded = new FakeRunEventSink();
        var publisher = Publisher([BestEffort(faulted, order: 0), BestEffort(succeeded, order: 1)], eventHub: hub);

        await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken);

        _ = faulted.Received.ShouldHaveSingleItem();
        _ = succeeded.Received.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task PublishAsync_WhenSinksAreRegisteredOutOfOrder_DeliversInDeclaredOrder()
    {
        await using var hub = Hub();
        var delivered = new List<string>();
        var first = new FakeRunEventSink((_, _) => { delivered.Add("first"); return ValueTask.CompletedTask; });
        var second = new FakeRunEventSink((_, _) => { delivered.Add("second"); return ValueTask.CompletedTask; });
        var publisher = Publisher([BestEffort(second, order: 5), BestEffort(first, order: 1)], eventHub: hub);

        await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken);

        delivered.ShouldBe(["first", "second"]);
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkBlocksAndThePolicyReturnsDrop_ThrowsInvalidOperationException()
    {
        await using var hub = Hub();
        var gate = new TaskCompletionSource();
        var sink = new FakeRunEventSink((_, _) => new ValueTask(gate.Task));
        var policy = new FakeOutputBackpressurePolicy(BackpressureDecision.Drop);
        var publisher = Publisher([Required(sink)], eventHub: hub, backpressurePolicy: policy);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken).AsTask());

        exception.Message.ShouldContain("Drop");
        _ = policy.Requests.ShouldHaveSingleItem();
        policy.Requests[0].Delivery.ShouldBe(RunEventDelivery.Required);
        _ = gate.TrySetResult();
    }

    [Fact]
    public async Task PublishAsync_WhenABestEffortSinkBlocksAndThePolicyReturnsDrop_StopsWaitingWithoutFailing()
    {
        await using var hub = Hub();
        var gate = new TaskCompletionSource();
        var sink = new FakeRunEventSink((_, _) => new ValueTask(gate.Task));
        var policy = new FakeOutputBackpressurePolicy(BackpressureDecision.Drop);
        var publisher = Publisher([BestEffort(sink)], eventHub: hub, backpressurePolicy: policy);

        await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken);

        policy.Requests.ShouldContain(request => request.Delivery == RunEventDelivery.BestEffort);
        _ = gate.TrySetResult();
    }

    [Fact]
    public async Task PublishAsync_WhenARequiredSinkEventuallyCompletes_WaitsAndDeliversSuccessfully()
    {
        await using var hub = Hub();
        var gate = new TaskCompletionSource();
        var sink = new FakeRunEventSink((_, _) => new ValueTask(gate.Task));
        var policy = new FakeOutputBackpressurePolicy(BackpressureDecision.Wait);
        var publisher = Publisher([Required(sink)], eventHub: hub, backpressurePolicy: policy, backpressurePollInterval: TimeSpan.FromMilliseconds(5));
        var publish = publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken).AsTask();

        await Task.Delay(20, TestContext.Current.CancellationToken);
        _ = gate.TrySetResult();
        await publish;

        _ = sink.Received.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CompleteAsync_WhenResultIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        await using var hub = Hub();
        var publisher = Publisher(eventHub: hub);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => publisher.CompleteAsync<string>(null!, TestContext.Current.CancellationToken).AsTask());
        exception.ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task CompleteAsync_WhenResultAddressesAnotherRun_ThrowsArgumentException()
    {
        await using var hub = Hub();
        var publisher = Publisher(eventHub: hub);
        var otherRun = new RunId(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var mismatched = new AgentRunFinished<string>(
            RunResultTestData.Agent, RunResultTestData.Session, null, otherRun, new RunSucceeded(), new RunSettlementCompleted(),
            "output", RunResultTestData.Cursor, [], new RunUsage(otherRun, []), [], ExtensionData.Empty);

        _ = await Should.ThrowAsync<ArgumentException>(
            () => publisher.CompleteAsync(mismatched, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task CompleteAsync_WhenCalledTwiceWithAnEquivalentResult_IsIdempotent()
    {
        await using var hub = Hub();
        var publisher = Publisher(eventHub: hub);
        var result = RunResultTestData.Finished();

        await publisher.CompleteAsync(result, TestContext.Current.CancellationToken);
        await publisher.CompleteAsync(result, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CompleteAsync_WhenCalledTwiceWithAConflictingResult_ThrowsInvalidOperationException()
    {
        await using var hub = Hub();
        var publisher = Publisher(eventHub: hub);
        await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);
        var conflicting = RunResultTestData.Finished(outcome: new RunIdle());

        _ = await Should.ThrowAsync<InvalidOperationException>(
            () => publisher.CompleteAsync(conflicting, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task CompleteAsync_SealsTheHubAgainstNewSubscriptions()
    {
        await using var hub = Hub();
        var publisher = Publisher(eventHub: hub);

        await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);

        _ = Should.Throw<RunEventSubscriptionRejectedException>(hub.Subscribe);
    }

    private static RunEventHub Hub() => new(
        RunResultTestData.Agent, RunResultTestData.Session, null, RunResultTestData.Run, new RunEventHubOptions(), TimeProvider.System);

    private static RunEventSinkBinding Required(IRunEventSink sink, int order = 0) =>
        new(new RunEventSinkRegistration("required", RunEventDelivery.Required, order), sink);

    private static RunEventSinkBinding BestEffort(IRunEventSink sink, int order = 0) =>
        new(new RunEventSinkRegistration($"best-effort-{order}", RunEventDelivery.BestEffort, order), sink);

    private static DefaultOutputPublisher Publisher(
        IEnumerable<IRunEventSink>? sinks = null,
        IOutputBackpressurePolicy? backpressurePolicy = null,
        RunEventHub? eventHub = null,
        TimeProvider? timeProvider = null,
        ILogger<DefaultOutputPublisher>? logger = null,
        TimeSpan backpressurePollInterval = default) =>
        new(
            sinks ?? [],
            backpressurePolicy ?? new FakeOutputBackpressurePolicy(BackpressureDecision.Wait),
            eventHub ?? Hub(),
            timeProvider ?? TimeProvider.System,
            logger ?? NullLogger<DefaultOutputPublisher>.Instance,
            backpressurePollInterval);
}
