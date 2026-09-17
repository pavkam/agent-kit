// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Defines reusable mandatory delivery, cancellation, identity, and deadline cases for <see cref="ISecurityAuditDispatcher"/>.</summary>
/// <typeparam name="TFixture">The independently composed fixture that supplies deterministic time.</typeparam>
public abstract class SecurityAuditDispatcherConformanceTests<TFixture>
    where TFixture : ISecurityAuditDispatcherConformanceFixture
{
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Creates an isolated fixture owned by one inherited conformance case.</summary>
    /// <returns>The fixture that composes one dispatcher through its supported public surface.</returns>
    protected abstract TFixture CreateFixture();

    /// <summary>Verifies required delivery stops before writing when no compatible durable sink exists.</summary>
    [Fact]
    public async Task DispatchAsync_WhenRequiredDurableAcceptanceIsUnavailable_ReturnsUnavailableWithoutWriting()
    {
        await using var fixture = CreateFixture();
        var unsupported = new RecordingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.BestEffort, durable: false, unsupported)],
            TestContext.Current.CancellationToken);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditUnavailable>();
        unsupported.Records.ShouldBeEmpty();
    }

    /// <summary>Verifies a throwing required durable sink returns a typed fail-closed result.</summary>
    [Fact]
    public async Task DispatchAsync_WhenRequiredSinkFails_ReturnsFailed()
    {
        await using var fixture = CreateFixture();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.Required, durable: true, new ThrowingSink())],
            TestContext.Current.CancellationToken);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditFailed>();
    }

    /// <summary>Verifies a best-effort sink failure cannot change successful best-effort delivery.</summary>
    [Fact]
    public async Task DispatchAsync_WhenOptionalSinkFails_ReturnsAccepted()
    {
        await using var fixture = CreateFixture();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.BestEffort,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.BestEffort, durable: false, new ThrowingSink())],
            TestContext.Current.CancellationToken);

        var result = await dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
    }

    /// <summary>Verifies caller cancellation propagates before any sink receives a record.</summary>
    [Fact]
    public async Task DispatchAsync_WhenCallerIsCancelledBeforeDelivery_PropagatesCancellationWithoutWriting()
    {
        await using var fixture = CreateFixture();
        var sink = new RecordingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.Required, durable: true, sink)],
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await dispatcher.DispatchAsync(Record(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        sink.Records.ShouldBeEmpty();
    }

    /// <summary>Verifies cancellation wins when a sink cancels the caller and throws a non-cancellation failure.</summary>
    [Fact]
    public async Task DispatchAsync_WhenRequiredSinkCancelsAndFails_PropagatesCallerCancellationWithoutInvokingLaterSink()
    {
        await using var fixture = CreateFixture();
        using var cancellation = new CancellationTokenSource();
        var laterSink = new RecordingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [
                Sink(SecurityAuditDelivery.Required, durable: true, new CancellingAndThrowingSink(cancellation)),
                Sink(SecurityAuditDelivery.Required, durable: true, laterSink),
            ],
            TestContext.Current.CancellationToken);

        var exception = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await dispatcher.DispatchAsync(Record(), cancellation.Token));

        exception.CancellationToken.ShouldBe(cancellation.Token);
        laterSink.Records.ShouldBeEmpty();
    }

    /// <summary>Verifies retries forward the original immutable record identity rather than minting another record.</summary>
    [Fact]
    public async Task DispatchAsync_WhenRecordIsRetried_PreservesTheExactRecordIdentity()
    {
        await using var fixture = CreateFixture();
        var sink = new RecordingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.Required, durable: true, sink)],
            TestContext.Current.CancellationToken);
        var record = Record();

        _ = await dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken);
        _ = await dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken);

        sink.Records.ShouldBe([record, record]);
        sink.Records.ShouldAllBe(value => value.Id == record.Id);
    }

    /// <summary>Verifies a cancellation-ignoring required sink is bounded and leaves durable acceptance ambiguous.</summary>
    [Fact]
    public async Task DispatchAsync_WhenRequiredSinkIgnoresDeadline_ReturnsTimedOutAndSkipsLaterSink()
    {
        await using var fixture = CreateFixture();
        var blocking = new BlockingSink();
        var laterSink = new RecordingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [
                Sink(SecurityAuditDelivery.Required, durable: true, blocking),
                Sink(SecurityAuditDelivery.Required, durable: true, laterSink),
            ],
            TestContext.Current.CancellationToken);

        var dispatch = dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        fixture.Advance(DeliveryTimeout);

        var result = await dispatch;

        _ = result.ShouldBeOfType<SecurityAuditTimedOut>();
        blocking.DeliveryCancellation.IsCancellationRequested.ShouldBeTrue();
        laterSink.Records.ShouldBeEmpty();
        blocking.Complete();
    }

    /// <summary>Verifies an optional timeout can be followed by a required durable acceptance.</summary>
    [Fact]
    public async Task DispatchAsync_WhenOptionalSinkTimesOutAndLaterRequiredSinkAccepts_ReturnsAccepted()
    {
        await using var fixture = CreateFixture();
        var blocking = new BlockingSink();
        var durable = new RecordingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [
                Sink(SecurityAuditDelivery.BestEffort, durable: false, blocking),
                Sink(SecurityAuditDelivery.Required, durable: true, durable),
            ],
            TestContext.Current.CancellationToken);

        var dispatch = dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        fixture.Advance(DeliveryTimeout);

        var result = await dispatch;

        _ = result.ShouldBeOfType<SecurityAuditAccepted>();
        _ = durable.Records.ShouldHaveSingleItem();
    }

    /// <summary>Verifies a timed-out optional durable sink remains an unknown required acceptance when no sink later accepts.</summary>
    [Fact]
    public async Task DispatchAsync_WhenOnlyOptionalDurableSinkTimesOutUnderRequiredPolicy_ReturnsTimedOut()
    {
        await using var fixture = CreateFixture();
        var blocking = new BlockingSink();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.BestEffort, durable: true, blocking)],
            TestContext.Current.CancellationToken);

        var dispatch = dispatcher.DispatchAsync(Record(), TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        fixture.Advance(DeliveryTimeout);

        var result = await dispatch;

        _ = result.ShouldBeOfType<SecurityAuditTimedOut>();
        blocking.Complete();
    }

    /// <summary>Verifies late sink completion preserves the original record and cannot revise the returned timeout result.</summary>
    [Fact]
    public async Task DispatchAsync_WhenTimedOutSinkCompletesLate_PreservesOneRecordIdentityAndOriginalTimeout()
    {
        await using var fixture = CreateFixture();
        var blocking = new BlockingSink();
        var record = Record();
        var dispatcher = await fixture.CreateAsync(
            SecurityAuditDelivery.Required,
            DeliveryTimeout,
            [Sink(SecurityAuditDelivery.Required, durable: true, blocking)],
            TestContext.Current.CancellationToken);

        var dispatch = dispatcher.DispatchAsync(record, TestContext.Current.CancellationToken).AsTask();
        await blocking.Started;
        fixture.Advance(DeliveryTimeout);
        var result = await dispatch;
        blocking.Complete();
        await blocking.Completion;
        await Task.Yield();

        _ = result.ShouldBeOfType<SecurityAuditTimedOut>();
        blocking.Records.ShouldBe([record]);
        blocking.Records[0].Id.ShouldBe(record.Id);
    }

    /// <summary>Verifies the fixture binding constructor rejects a missing registration or sink before retaining either.</summary>
    [Fact]
    public void SecurityAuditDispatcherConformanceSink_WhenRegistrationOrSinkIsNull_ThrowsWithExactParameterNames()
    {
        var registration = new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], SecurityAuditDelivery.BestEffort, providesDurableAcceptance: false);
        var sink = new RecordingSink();

        var invalidRegistration = Should.Throw<ArgumentNullException>(() => new SecurityAuditDispatcherConformanceSink(null!, sink));
        var invalidSink = Should.Throw<ArgumentNullException>(() => new SecurityAuditDispatcherConformanceSink(registration, null!));

        invalidRegistration.ParamName.ShouldBe("registration");
        invalidSink.ParamName.ShouldBe("sink");
    }

    /// <summary>Verifies copying the fixture binding without changes retains its exact registration and sink.</summary>
    [Fact]
    public void SecurityAuditDispatcherConformanceSink_WhenCopyingWithoutChanges_RetainsTheOriginalRegistrationAndSink()
    {
        var original = Sink(SecurityAuditDelivery.BestEffort, durable: false, new RecordingSink());

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }

    /// <summary>Creates one public fixture binding for a sink with support for the standard grant-consumption event.</summary>
    /// <param name="delivery">The binding's required or best-effort delivery class.</param>
    /// <param name="durable">Whether successful completion proves durable acceptance.</param>
    /// <param name="sink">The fixture-owned sink instance.</param>
    /// <returns>The immutable conformance binding.</returns>
    private static SecurityAuditDispatcherConformanceSink Sink(
        SecurityAuditDelivery delivery,
        bool durable,
        ISecurityAuditSink sink) => new(
        new SecurityAuditSinkRegistration([SecurityAuditEventKind.GrantConsumptionIntent], delivery, durable), sink);

    /// <summary>Creates deterministic redacted record evidence used by every portable delivery case.</summary>
    /// <returns>A valid immutable record with a stable identity and no protected field content.</returns>
    private static SecurityAuditRecord Record() => new(
        new SecurityAuditRecordId(Guid.Parse("a1111111-1111-1111-1111-111111111111")),
        new SecurityAuthorizationScope(
            new AgentId(Guid.Parse("a2222222-2222-2222-2222-222222222222")),
            new SessionId(Guid.Parse("a3333333-3333-3333-3333-333333333333")),
            new BeforeRunOperationCorrelation(
                new OperationId(Guid.Parse("a4444444-4444-4444-4444-444444444444")),
                new AdmissionId(Guid.Parse("a5555555-5555-5555-5555-555555555555")))),
        new SecurityRequestId(Guid.Parse("a6666666-6666-6666-6666-666666666666")),
        new GrantId(Guid.Parse("a7777777-7777-7777-7777-777777777777")),
        null,
        SecurityAuditEventKind.GrantConsumptionIntent,
        SecurityAuditOutcome.Accepted,
        new SecurityPolicyVersion(1),
        [],
        DateTimeOffset.UnixEpoch);

    private sealed class RecordingSink: ISecurityAuditSink
    {
        public List<SecurityAuditRecord> Records { get; } = [];

        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingSink: ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromException(new InvalidOperationException("sink"));
    }

    private sealed class CancellingAndThrowingSink(CancellationTokenSource cancellation): ISecurityAuditSink
    {
        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromException(new InvalidOperationException("sink"));
        }
    }

    private sealed class BlockingSink: ISecurityAuditSink
    {
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _completion.Task;

        public CancellationToken DeliveryCancellation { get; private set; }

        public List<SecurityAuditRecord> Records { get; } = [];

        public Task Started => _started.Task;

        public void Complete() => _completion.TrySetResult(true);

        public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            DeliveryCancellation = cancellationToken;
            _ = _started.TrySetResult(true);
            return new ValueTask(_completion.Task);
        }
    }
}
