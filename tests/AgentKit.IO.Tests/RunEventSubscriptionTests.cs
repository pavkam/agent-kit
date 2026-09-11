// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using static AgentKit.IO.Tests.RunEventHubTests;

public sealed class RunEventSubscriptionTests
{
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
        var basis = Event(1);
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
                    _ = await hub.PublishAsync(Event(2), TestContext.Current.CancellationToken);
                    _ = await hub.PublishAsync(Event(3), TestContext.Current.CancellationToken);
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
            (await hub.PublishAsync(Event(4), TestContext.Current.CancellationToken)).ShouldBe(RunEventPublicationOutcome.Published);
        }
    }
}
