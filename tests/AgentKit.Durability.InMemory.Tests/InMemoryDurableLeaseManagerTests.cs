// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

public sealed class InMemoryDurableLeaseManagerTests
{
    private static readonly DurableOperationAddress Address = new(
        new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000001")),
        new RunId(Guid.Parse("30000000-0000-0000-0000-000000000001")),
        new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000001")));

    private static readonly WorkerId WorkerA = new(Guid.Parse("50000000-0000-0000-0000-00000000000a"));
    private static readonly WorkerId WorkerB = new(Guid.Parse("50000000-0000-0000-0000-00000000000b"));
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData("leaseIds")]
    [InlineData("timeProvider")]
    public void Constructor_WhenADependencyIsNull_RejectsExactArgument(string parameter)
    {
        var exception = Should.Throw<ArgumentNullException>(() => new InMemoryDurableLeaseManager(
            parameter == "leaseIds" ? null! : new GuidExecutionLeaseIdGenerator(),
            parameter == "timeProvider" ? null! : new FakeTimeProvider(Epoch)));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public async Task AcquireAsync_WhenRequestIsNull_RejectsExactArgument()
    {
        var manager = Manager();

        (await Should.ThrowAsync<ArgumentNullException>(async () =>
            await manager.AcquireAsync(null!, TestContext.Current.CancellationToken)))
            .ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task AcquireAsync_WhenNoPriorGenerationExists_GrantsOwnership()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);

        var result = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        var acquired = result.ShouldBeOfType<ExecutionLeaseAcquired>();
        await using var lease = acquired.Lease;
        lease.OwnerWorkerId.ShouldBe(WorkerA);
        lease.Address.ShouldBe(Address);
        lease.FencingToken.ShouldBe(new FencingToken(1));
        lease.ExpiresAt.ShouldBe(Epoch + TimeSpan.FromMinutes(1));
        lease.LeaseId.ShouldNotBe(default);
    }

    [Fact]
    public async Task AcquireAsync_WhenAnUnexpiredGenerationIsHeld_RefusesWithoutWaiting()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var first = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        var second = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        var busy = second.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>();
        busy.CurrentOwnerWorkerId.ShouldBe(WorkerA);
        busy.CurrentToken.ShouldBe(firstLease.FencingToken);
        busy.ExpiresAt.ShouldBe(Epoch + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AcquireAsync_WhenTheSameWorkerStillHoldsAnUnexpiredLease_IsAlsoRefused()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var first = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        var second = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        second.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>().CurrentOwnerWorkerId.ShouldBe(WorkerA);
    }

    [Fact]
    public async Task AcquireAsync_WhenThePriorGenerationHasExpired_GrantsTakeoverWithANewToken()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var first = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.Advance(TimeSpan.FromMinutes(2));

        var second = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        var acquired = second.ShouldBeOfType<ExecutionLeaseAcquired>();
        await using var secondLease = acquired.Lease;
        secondLease.OwnerWorkerId.ShouldBe(WorkerB);
        secondLease.FencingToken.ShouldBeGreaterThan(firstLease.FencingToken);
        secondLease.ExpiresAt.ShouldBe(Epoch + TimeSpan.FromMinutes(3));
    }

    [Fact]
    public async Task AcquireAsync_WhenDisposedAndReacquired_GrantsANewTokenImmediately()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var first = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        await firstLease.DisposeAsync();

        var second = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        var acquired = second.ShouldBeOfType<ExecutionLeaseAcquired>();
        await using var secondLease = acquired.Lease;
        secondLease.OwnerWorkerId.ShouldBe(WorkerB);
        secondLease.FencingToken.ShouldBeGreaterThan(firstLease.FencingToken);
    }

    [Fact]
    public async Task AcquireAsync_WhenAddressesDiffer_GrantsIndependentGenerations()
    {
        var manager = Manager();
        var other = new DurableOperationAddress(Address.AgentId, Address.SessionId, Address.RunId, new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000002")));

        var first = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var second = await manager.AcquireAsync(new ExecutionLeaseRequest(other, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        await using var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        await using var secondLease = second.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        firstLease.FencingToken.ShouldNotBe(secondLease.FencingToken);
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelledBeforeAcquisition_GrantsNothing()
    {
        var manager = Manager();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), cancellation.Token));

        var result = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var acquired = result.ShouldBeOfType<ExecutionLeaseAcquired>();
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenTheGeneratorProducesADefaultIdentity_FailsBeforeGrantingOwnership()
    {
        var manager = Manager(leaseIds: new FixedLeaseIdGenerator(default));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken));

        var recovered = await Manager().AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await recovered.ShouldBeOfType<ExecutionLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenElapsedTimeMeasurementFails_StillGrantsOwnership()
    {
        // GetTimestamp succeeds for TryGetTimestamp's own call (unlike ThrowingTimestampTimeProvider
        // below), so TryGetElapsedTime's independent failure boundary around its own GetTimestamp
        // call is exercised instead of short-circuiting before ever calling it.
        var manager = Manager(new ThrowingElapsedTimeProvider());

        var result = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ExecutionLeaseAcquired>();
    }

    [Fact]
    public async Task AcquireAsync_WhenTheClockAndLoggerFail_StillGrantsOwnership()
    {
        var manager = new InMemoryDurableLeaseManager(
            new GuidExecutionLeaseIdGenerator(), new ThrowingTimestampTimeProvider(), new ThrowingLogger<InMemoryDurableLeaseManager>());

        var result = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        await result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_WhenCancelledAndLoggingIsEnabled_RecordsCancellationEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableLeaseManager>();
        var manager = new InMemoryDurableLeaseManager(new GuidExecutionLeaseIdGenerator(), new FakeTimeProvider(Epoch), recorder);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), cancellation.Token));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 20001 && entry.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task AcquireAsync_WhenGeneratorFailsAndLoggingIsEnabled_RecordsFailureEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableLeaseManager>();
        var manager = new InMemoryDurableLeaseManager(new FixedLeaseIdGenerator(default), new FakeTimeProvider(Epoch), recorder);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 20002 && entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RenewAsync_WhenCancelledAndLoggingIsEnabled_RecordsCancellationEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableLeaseManager>();
        var manager = new InMemoryDurableLeaseManager(new GuidExecutionLeaseIdGenerator(), new FakeTimeProvider(Epoch), recorder);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await lease.RenewAsync(cancellation.Token));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 20004 && entry.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task RenewAsync_WhenClockThrowsAndLoggingIsEnabled_RecordsFailureEvent()
    {
        var recorder = new RecordingLogger<InMemoryDurableLeaseManager>();
        var clock = new ToggleClockTimeProvider(Epoch);
        var manager = new InMemoryDurableLeaseManager(new GuidExecutionLeaseIdGenerator(), clock, recorder);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.ThrowOnGetUtcNow = true;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await lease.RenewAsync(TestContext.Current.CancellationToken));

        recorder.Snapshot().ShouldContain(entry => entry.EventId.Id == 20005 && entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RenewAsync_WhenPresentedTokenIsStillCurrent_ExtendsExpiryByTheOriginalDuration()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.Advance(TimeSpan.FromSeconds(30));

        var renewal = await lease.RenewAsync(TestContext.Current.CancellationToken);

        var renewed = renewal.ShouldBeOfType<LeaseRenewed>();
        renewed.ExpiresAt.ShouldBe(Epoch + TimeSpan.FromSeconds(30) + TimeSpan.FromMinutes(1));
        lease.ExpiresAt.ShouldBe(renewed.ExpiresAt);
        lease.FencingToken.ShouldBe(new FencingToken(1));
    }

    [Fact]
    public async Task RenewAsync_WhenAnotherWorkerHasTakenOver_ReturnsLostWithTheCurrentToken()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var first = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.Advance(TimeSpan.FromMinutes(2));
        var second = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var secondLease = second.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        var renewal = await firstLease.RenewAsync(TestContext.Current.CancellationToken);

        var lost = renewal.ShouldBeOfType<LeaseLost>();
        lost.CurrentToken.ShouldBe(secondLease.FencingToken);
    }

    [Fact]
    public async Task RenewAsync_WhenLeaseHasExpiredButNoOneTookOver_DoesNotResurrectOwnership()
    {
        // durable-execution-and-recovery.md: the lease service is authoritative for expiry; a worker that missed its
        // renewal window has lost ownership even if nobody else acquired yet. Renewing must not silently resurrect it.
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.Advance(TimeSpan.FromMinutes(5));

        var renewal = await lease.RenewAsync(TestContext.Current.CancellationToken);

        _ = renewal.ShouldBeOfType<LeaseLost>();
    }

    [Fact]
    public async Task RenewAsync_WhenLeaseIsAlreadyDisposed_ThrowsObjectDisposedException()
    {
        var manager = Manager();
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        await lease.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await lease.RenewAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RenewAsync_WhenCancelledBeforeRenewal_DoesNotExtendExpiry()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        var originalExpiry = lease.ExpiresAt;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await lease.RenewAsync(cancellation.Token));

        lease.ExpiresAt.ShouldBe(originalExpiry);
    }

    [Fact]
    public async Task RenewAsync_WhenClockThrowsUnexpectedly_PropagatesAndReportsFailedOutcome()
    {
        var clock = new ToggleClockTimeProvider(Epoch);
        var manager = Manager(clock);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.ThrowOnGetUtcNow = true;

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await lease.RenewAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldBe("clock failure during renewal");
    }

    [Fact]
    public async Task RenewAsync_WhenTheClockAndLoggerFail_StillReportsRenewal()
    {
        var manager = new InMemoryDurableLeaseManager(
            new GuidExecutionLeaseIdGenerator(), new FakeTimeProvider(Epoch));
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        var throwingManager = new InMemoryDurableLeaseManager(
            new GuidExecutionLeaseIdGenerator(), new ThrowingTimestampTimeProvider(), new ThrowingLogger<InMemoryDurableLeaseManager>());
        var throwingAcquired = await throwingManager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var throwingLease = throwingAcquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        var renewal = await throwingLease.RenewAsync(TestContext.Current.CancellationToken);

        _ = renewal.ShouldBeOfType<LeaseRenewed>();
    }

    [Fact]
    public async Task AcquireAsync_WhenElapsedTimeIsNegative_StillGrantsOwnershipDespiteMetricsFailure()
    {
        // DurableLeaseMetrics.RecordAcquisition rejects a negative elapsed duration. Producing one from a
        // clock that never throws exercises FinishAcquisition's own metrics-recording catch, distinct from
        // TryGetElapsedTime's catch around the clock call itself (covered above via ThrowingElapsedTimeProvider).
        var manager = Manager(new NegativeElapsedTimeProvider());

        var result = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        await result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease.DisposeAsync();
    }

    [Fact]
    public async Task RenewAsync_WhenElapsedTimeIsNegative_StillReportsRenewalDespiteMetricsFailure()
    {
        // Mirrors the acquisition case above for FinishRenewal's own metrics-recording catch.
        var clock = new NegativeElapsedTimeProvider();
        var manager = Manager(clock);
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await using var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        var renewal = await lease.RenewAsync(TestContext.Current.CancellationToken);

        _ = renewal.ShouldBeOfType<LeaseRenewed>();
    }

    [Fact]
    public async Task Duration_WhenALeaseIsAcquired_ReportsTheRequestedDurationUnchanged()
    {
        var manager = Manager();

        var result = await manager.AcquireAsync(
            new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(7)), TestContext.Current.CancellationToken);

        var lease = (InMemoryExecutionLease) result.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        lease.Duration.ShouldBe(TimeSpan.FromMinutes(7));
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenStillTheCurrentGeneration_ReleasesItImmediately()
    {
        var manager = Manager();
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(30)), TestContext.Current.CancellationToken);
        var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        await lease.DisposeAsync();
        var reacquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        var secondLease = reacquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        await secondLease.DisposeAsync();
        secondLease.OwnerWorkerId.ShouldBe(WorkerB);
    }

    [Fact]
    public async Task DisposeAsync_WhenAlreadyLostToTakeover_DoesNotReleaseTheNewOwner()
    {
        var clock = new FakeTimeProvider(Epoch);
        var manager = Manager(clock);
        var first = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var firstLease = first.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        clock.Advance(TimeSpan.FromMinutes(2));
        var second = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var secondLease = second.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        await firstLease.DisposeAsync();
        var stillHeld = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);

        stillHeld.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>().CurrentToken.ShouldBe(secondLease.FencingToken);
        await secondLease.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_WhenRepeated_IsHarmless()
    {
        var manager = Manager();
        var acquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerA, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        var lease = acquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        var reacquired = await manager.AcquireAsync(new ExecutionLeaseRequest(Address, WorkerB, TimeSpan.FromMinutes(1)), TestContext.Current.CancellationToken);
        await reacquired.ShouldBeOfType<ExecutionLeaseAcquired>().Lease.DisposeAsync();
    }

    private static InMemoryDurableLeaseManager Manager(
        TimeProvider? clock = null, IIdentifierGenerator<ExecutionLeaseId>? leaseIds = null) =>
        new(leaseIds ?? new GuidExecutionLeaseIdGenerator(), clock ?? new FakeTimeProvider(Epoch));

    private sealed class FixedLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
    {
        private readonly ExecutionLeaseId _value;

        internal FixedLeaseIdGenerator(ExecutionLeaseId value) => _value = value;

        public ExecutionLeaseId Create() => _value;
    }

    private sealed class ThrowingTimestampTimeProvider: TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Epoch;

        public override long GetTimestamp() => throw new InvalidTimeZoneException("clock failure");
    }

    private sealed class ToggleClockTimeProvider(DateTimeOffset now): TimeProvider
    {
        internal bool ThrowOnGetUtcNow { get; set; }

        public override DateTimeOffset GetUtcNow() =>
            ThrowOnGetUtcNow ? throw new InvalidOperationException("clock failure during renewal") : now;
    }

    private sealed class ThrowingElapsedTimeProvider: TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => Epoch;

        public override long GetTimestamp() => ++_calls == 1 ? 1 : throw new InvalidTimeZoneException("elapsed-time failure");
    }

    /// <summary>A clock that never throws but reports a negative elapsed duration, which the bounded metrics
    /// recorders reject; this exercises the manager's own metrics-recording catch rather than its clock-failure catch.
    /// Each operation calls <see cref="GetTimestamp"/> once at its start and once more internally through the
    /// default <see cref="TimeProvider.GetElapsedTime(long)"/> at its finish; alternating a later raw value on the
    /// first call of each pair with an earlier one on the second makes every operation's derived elapsed duration
    /// negative, without either call ever throwing.</summary>
    private sealed class NegativeElapsedTimeProvider: TimeProvider
    {
        private int _calls;

        public override DateTimeOffset GetUtcNow() => Epoch;

        public override long GetTimestamp() => ++_calls % 2 == 1 ? 1000 : 0;
    }

    private sealed class ThrowingLogger<TCategory>: ILogger<TCategory>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidTimeZoneException("logger failure");
    }
}
