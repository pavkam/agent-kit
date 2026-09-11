// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

public sealed class RunEventHubTests
{
    private static readonly AgentId Agent = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly SessionId Session = new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static readonly RunId Run = new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static readonly TurnId Turn = new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static readonly MessageId Message = new(Guid.Parse("50000000-0000-0000-0000-000000000001"));

    [Theory]
    [InlineData(0, 1, "maximumSubscriptions")]
    [InlineData(-1, 1, "maximumSubscriptions")]
    [InlineData(1, 0, "capacityPerSubscription")]
    [InlineData(1, -1, "capacityPerSubscription")]
    public void Constructor_WhenBoundsAreInvalid_ThrowsExactArgumentOutOfRange(int subscribers, int capacity, string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventHubOptions(subscribers, capacity));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(parameter);
    }

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
    public async Task ReadAllAsync_WhenRegisteredBeforeReading_DrainsAcceptedEventsInOrderAfterCompletion()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        var first = Event(1);
        var second = Event(2);
        (await hub.PublishAsync(first, TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        (await hub.PublishAsync(second, TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        hub.Complete();
        await hub.DisposeAsync();

        var events = await ReadAsync(subscription);

        events.ShouldBe([first, second]);
        events[0].ShouldBeSameAs(first);
        events[1].ShouldBeSameAs(second);
    }

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

    [Fact]
    public async Task ReadAllAsync_WhenCancelled_ReleasesSubscriptionWithoutClosingHub()
    {
        await using var hub = Hub(subscribers: 1);
        await using var subscription = hub.Subscribe();
        using var cancellation = new CancellationTokenSource();
        await using var reader = subscription.ReadAllAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var pending = reader.MoveNextAsync().AsTask();
        cancellation.Cancel();
        (await Should.ThrowAsync<OperationCanceledException>(() => pending)).CancellationToken.ShouldBe(cancellation.Token);
        subscription.State.ShouldBe(RunEventSubscriptionState.Cancelled);
        await using var replacement = hub.Subscribe();
        (await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
    }

    [Fact]
    public async Task ReadAllAsync_WhenAlreadyCancelled_ReleasesRegistrationAtFirstMove()
    {
        await using var hub = Hub(subscribers: 1);
        await using var subscription = hub.Subscribe();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var reader = subscription.ReadAllAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => reader.MoveNextAsync().AsTask());
        await using var replacement = hub.Subscribe();
    }

    [Fact]
    public async Task ReadAllAsync_WhenEnumerationIsAbandoned_ReleasesRemainingBufferAndRegistration()
    {
        await using var hub = Hub(subscribers: 1);
        var subscription = hub.Subscribe();
        _ = await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken);
        _ = await hub.PublishAsync(Event(2), TestContext.Current.CancellationToken);
        await foreach (var item in subscription.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            item.Sequence.ShouldBe(1);
            break;
        }
        subscription.State.ShouldBe(RunEventSubscriptionState.Disposed);
        await using var replacement = hub.Subscribe();
    }

    [Fact]
    public async Task ReadAllAsync_WhenSecondEnumerationStarts_RejectsWithoutClosingFirst()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        await using var first = subscription.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        await using var second = subscription.ReadAllAsync(TestContext.Current.CancellationToken).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var pending = first.MoveNextAsync().AsTask();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => second.MoveNextAsync().AsTask());
        _ = await hub.PublishAsync(Event(1), TestContext.Current.CancellationToken);
        (await pending).ShouldBeTrue();
        first.Current.Sequence.ShouldBe(1);
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenSubscriptionCapacityIsInvalid_RejectsBeforeCallbacks(int capacity)
    {
        var calls = 0;
        var error = Should.Throw<ArgumentOutOfRangeException>(() => new RunEventSubscription(capacity,
            _ => calls++, _ => throw new InvalidOperationException("observe")));
        error.ParamName.ShouldBe("capacity");
        calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(true, "release")]
    [InlineData(false, "observe")]
    public void Constructor_WhenSubscriptionCallbackIsNull_RejectsExactArgument(bool nullRelease, string parameter)
    {
        var error = Should.Throw<ArgumentNullException>(() => new RunEventSubscription(1,
            nullRelease ? null! : _ => { }, nullRelease ? _ => throw new InvalidOperationException() : null!));
        error.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public async Task Offer_WhenEventIsNull_RejectsExactArgument()
    {
        await using var hub = Hub();
        await using var subscription = hub.Subscribe();
        Should.Throw<ArgumentNullException>(() => subscription.Offer(null!)).ParamName.ShouldBe("runEvent");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void Constructor_WhenSubscriptionRejectionIsUndefined_RejectsExactArgument(int reason) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunEventSubscriptionRejectedException((RunEventSubscriptionRejection) reason)).ParamName.ShouldBe("reason");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(-1)]
    public void Constructor_WhenClosureIsNotDeliveryFailure_RejectsExactArgument(int state) =>
        Should.Throw<ArgumentException>(() => new RunEventSubscriptionClosedException((RunEventSubscriptionState) state, null)).ParamName.ShouldBe("state");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenUnavailableSequenceIsInvalid_RejectsExactArgument(long sequence) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RunEventSubscriptionClosedException(RunEventSubscriptionState.SlowConsumer, sequence)).ParamName.ShouldBe("firstUnavailableSequence");

    internal static RunEventHub Hub(int subscribers = 2, int capacity = 2) => new(Agent, Session, null, Run, new RunEventHubOptions(subscribers, capacity), TimeProvider.System);
    internal static MessageCommittedEvent Event(long sequence) => new(Agent, Session, null, Run, Turn, sequence, DateTimeOffset.UnixEpoch, Message, new SessionVersion(1));

    private static async Task<List<RunEvent>> ReadAsync(RunEventSubscription subscription)
    {
        List<RunEvent> events = [];
        await foreach (var item in subscription.ReadAllAsync(TestContext.Current.CancellationToken)) { events.Add(item); }
        return events;
    }
}
