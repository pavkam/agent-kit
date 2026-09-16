// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.TestSupport;

public sealed class AgentRunOutputPublisherTests
{
    private static readonly AgentId Agent = RunResultTestData.Agent;
    private static readonly SessionId Session = RunResultTestData.Session;
    private static readonly RunId Run = RunResultTestData.Run;
    private static readonly TurnId Turn = RunResultTestData.Turn;
    private static readonly RunId ForeignRun = new(Guid.Parse("3000000f-0000-0000-0000-000000000001"));

    [Theory]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("conversationId")]
    [InlineData("runId")]
    public void Constructor_WhenIdentityIsDefault_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentRunOutputPublisher(
            parameter == "agentId" ? default : Agent,
            parameter == "sessionId" ? default : Session,
            parameter == "conversationId" ? default(ConversationId) : null,
            parameter == "runId" ? default : Run,
            TimeProvider.System));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenClockIsNull_RejectsExactArgument() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunOutputPublisher(Agent, Session, null, Run, null!))
            .ParamName.ShouldBe("timeProvider");

    [Fact]
    public async Task PublishAsync_WhenEventIsNull_RejectsExactArgument()
    {
        await using var publisher = Publisher();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await publisher.PublishAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("runEvent");
    }

    [Fact]
    public async Task PublishAsync_WhenEventAddressesAnotherRun_RejectsExactArgument()
    {
        await using var publisher = Publisher();

        (await Should.ThrowAsync<ArgumentException>(async () =>
            await publisher.PublishAsync(ForeignEvent(1), TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("runEvent");
    }

    [Fact]
    public async Task PublishAsync_WhenSequenceDoesNotAdvance_RejectsWithoutDeliveringTheEvent()
    {
        await using var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            var first = RunResultTestData.Event(2);
            await publisher.PublishAsync(first, TestContext.Current.CancellationToken);

            (await Should.ThrowAsync<ArgumentException>(async () =>
                await publisher.PublishAsync(RunResultTestData.Event(2), TestContext.Current.CancellationToken)))
                .ParamName.ShouldBe("runEvent");
            (await Should.ThrowAsync<ArgumentException>(async () =>
                await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken)))
                .ParamName.ShouldBe("runEvent");

            await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);
            (await ReadAsync(stream)).ShouldBe([first]);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenCancelledBeforeAcceptance_PropagatesCancellationWithoutDelivery()
    {
        await using var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
                await publisher.PublishAsync(RunResultTestData.Event(1), cancellation.Token));

            await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);
            (await ReadAsync(stream)).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task PublishAsync_WhenSubscriberIsRegistered_DeliversAdvancingEventsInOrder()
    {
        await using var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            var first = RunResultTestData.Event(1);
            var second = RunResultTestData.Event(2);
            await publisher.PublishAsync(first, TestContext.Current.CancellationToken);
            await publisher.PublishAsync(second, TestContext.Current.CancellationToken);
            await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);

            (await ReadAsync(stream)).ShouldBe([first, second]);
        }
    }

    [Fact]
    public async Task PublishAsync_WhenFinalResultWasExposed_RefusesFurtherEvents()
    {
        await using var publisher = Publisher();
        await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteAsync_WhenResultIsNull_RejectsExactArgument()
    {
        await using var publisher = Publisher();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await publisher.CompleteAsync<string>(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task CompleteAsync_WhenResultAddressesAnotherRun_RejectsExactArgument()
    {
        await using var publisher = Publisher();

        (await Should.ThrowAsync<ArgumentException>(async () =>
            await publisher.CompleteAsync(ForeignFinished(), TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task CompleteAsync_WhenCancelledBeforeAcceptance_ExposesNoEnvelope()
    {
        await using var publisher = Publisher();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await publisher.CompleteAsync(RunResultTestData.Finished(), cancellation.Token));

        // Acceptance never happened, so ordinary publication still works afterwards.
        await publisher.PublishAsync(RunResultTestData.Event(1), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CompleteAsync_WhenEnvelopeIsExposed_CompletesTheTypedWaitAndSealsTheEventStream()
    {
        await using var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            var finished = RunResultTestData.Finished();
            await publisher.CompleteAsync(finished, TestContext.Current.CancellationToken);

            (await stream.Completion).ShouldBe(finished);
            (await ReadAsync(stream)).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task CompleteAsync_WhenRepeatedWithAnEquivalentEnvelope_IsIdempotent()
    {
        await using var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);
            await publisher.CompleteAsync(RunResultTestData.Finished(), TestContext.Current.CancellationToken);

            (await stream.Completion).ShouldBe(RunResultTestData.Finished());
        }
    }

    [Fact]
    public async Task CompleteAsync_WhenAConflictingEnvelopeArrives_RetainsTheOriginal()
    {
        await using var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            var original = RunResultTestData.Finished();
            await publisher.CompleteAsync(original, TestContext.Current.CancellationToken);

            _ = await Should.ThrowAsync<InvalidOperationException>(async () => await publisher.CompleteAsync(
                RunResultTestData.Finished(messages: [RunResultTestData.Message()]),
                TestContext.Current.CancellationToken));

            (await stream.Completion).ShouldBe(original);
        }
    }

    [Fact]
    public async Task CompleteAsync_WhenTheRunIsBoundToAnotherOutputType_RejectsExactArgument()
    {
        await using var publisher = Publisher();
        await using var stream = publisher.Subscribe<string>();

        (await Should.ThrowAsync<ArgumentException>(async () =>
            await publisher.CompleteAsync(TypedFinished(42), TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("result");
    }

    [Fact]
    public async Task Subscribe_WhenTheRunIsAlreadyBoundToAnotherOutputType_Refuses()
    {
        await using var publisher = Publisher();
        await using var stream = publisher.Subscribe<string>();

        _ = Should.Throw<InvalidOperationException>(publisher.Subscribe<int>);
    }

    [Fact]
    public async Task Subscribe_WhenTheSubscriberBoundIsReached_RejectsUntilARegistrationIsReleased()
    {
        await using var publisher = Publisher(subscriptions: 1);
        var first = publisher.Subscribe<string>();

        Should.Throw<RunEventSubscriptionRejectedException>(publisher.Subscribe<string>)
            .Reason.ShouldBe(RunEventSubscriptionRejection.CapacityReached);

        await first.DisposeAsync();
        await using var replacement = publisher.Subscribe<string>();
        replacement.RunId.ShouldBe(Run);
    }

    [Fact]
    public async Task Subscribe_WhenRegistered_ExposesTheRunCorrelationOfThisPublisher()
    {
        await using var publisher = Publisher();
        await using var stream = publisher.Subscribe<string>();

        stream.AgentId.ShouldBe(Agent);
        stream.SessionId.ShouldBe(Session);
        stream.ConversationId.ShouldBeNull();
        stream.RunId.ShouldBe(Run);
    }

    [Fact]
    public async Task DisposeAsync_WhenNoEnvelopeWasExposed_CancelsPendingFinalResultWaitsWithoutFabricatingAnOutcome()
    {
        var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            await publisher.DisposeAsync();

            _ = await Should.ThrowAsync<OperationCanceledException>(async () => await stream.Completion);
        }
    }

    [Fact]
    public async Task DisposeAsync_WhenTheEnvelopeWasAlreadyExposed_PreservesItAcrossRepeatedDisposal()
    {
        var publisher = Publisher();
        var stream = publisher.Subscribe<string>();
        await using (stream)
        {
            var finished = RunResultTestData.Finished();
            await publisher.CompleteAsync(finished, TestContext.Current.CancellationToken);

            await publisher.DisposeAsync();
            await publisher.DisposeAsync();

            (await stream.Completion).ShouldBe(finished);
        }
    }

    [Fact]
    public async Task CompleteAsync_WhenTheLoggerFails_StillExposesTheEnvelope()
    {
        await using var publisher = Publisher(logger: new ThrowingLogger<AgentRunOutputPublisher>());
        var finished = RunResultTestData.Finished();

        await publisher.CompleteAsync(finished, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await publisher.CompleteAsync(
            RunResultTestData.Finished(messages: [RunResultTestData.Message()]),
            TestContext.Current.CancellationToken));
    }

    private static AgentRunOutputPublisher Publisher(int subscriptions = 32, ILogger<AgentRunOutputPublisher>? logger = null) =>
        new(Agent, Session, null, Run, TimeProvider.System, new RunOutputPublisherOptions(subscriptions), logger);

    private sealed class ThrowingLogger<TCategory>: ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidTimeZoneException("logger failure");
    }

    private static MessageCommittedEvent ForeignEvent(long sequence) => new(
        Agent, Session, null, ForeignRun, Turn, sequence, DateTimeOffset.UnixEpoch,
        RunResultTestData.Message().Id, new SessionVersion(sequence));

    private static AgentRunFinished<string> ForeignFinished() => new(
        Agent, Session, null, ForeignRun, new RunSucceeded(), new RunSettlementCompleted(), "output",
        RunResultTestData.Cursor, [], new RunUsage(ForeignRun, []), [], ExtensionData.Empty);

    private static AgentRunFinished<int> TypedFinished(int output) => new(
        Agent, Session, null, Run, new RunSucceeded(), new RunSettlementCompleted(), output,
        RunResultTestData.Cursor, [], new RunUsage(Run, []), [], ExtensionData.Empty);

    private static async Task<IReadOnlyList<RunEvent>> ReadAsync(IAgentRunStream<string> stream)
    {
        var received = new List<RunEvent>();
        await foreach (var runEvent in stream.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            received.Add(runEvent);
        }
        return received;
    }
}
