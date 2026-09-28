// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

/// <summary>Verifies clock-driven lease renewal, loss reporting, and failure isolation.</summary>
public sealed class ExecutionLeaseRenewalTests
{
    [Fact]
    public void Constructor_WhenTheLeaseIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ExecutionLeaseRenewal(
                null!,
                new FakeTimeProvider(DurableJournalTestData.Now),
                TimeSpan.FromSeconds(1),
                NullLogger.Instance));

        exception.ParamName.ShouldBe("lease");
    }

    [Fact]
    public void Constructor_WhenTheTimeProviderIsNull_ThrowsArgumentNullException()
    {
        var lease = Lease();

        var exception = Should.Throw<ArgumentNullException>(
            () => new ExecutionLeaseRenewal(lease, null!, TimeSpan.FromSeconds(1), NullLogger.Instance));

        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public void Constructor_WhenTheLoggerIsNull_ThrowsArgumentNullException()
    {
        var lease = Lease();

        var exception = Should.Throw<ArgumentNullException>(
            () => new ExecutionLeaseRenewal(
                lease,
                new FakeTimeProvider(DurableJournalTestData.Now),
                TimeSpan.FromSeconds(1),
                null!));

        exception.ParamName.ShouldBe("logger");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenTheIntervalIsNotPositive_ThrowsArgumentOutOfRangeException(int seconds)
    {
        var lease = Lease();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new ExecutionLeaseRenewal(
                lease,
                new FakeTimeProvider(DurableJournalTestData.Now),
                TimeSpan.FromSeconds(seconds),
                NullLogger.Instance));

        exception.ParamName.ShouldBe("interval");
    }

    [Fact]
    public void Constructor_WhenTheIntervalHasNotElapsed_DoesNotRenew()
    {
        // Renewal runs on the injected clock, never on a wall-clock delay.
        var lease = Lease();
        var time = new FakeTimeProvider(DurableJournalTestData.Now);

        using var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);

        lease.Renewals.ShouldBe(0);
        renewal.Lost.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_WhenTheClockAdvancesAfterwards_StopsRenewing()
    {
        var lease = Lease();
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);
        time.Advance(TimeSpan.FromSeconds(20));
        var renewedBeforeDisposal = lease.Renewals;

        renewal.Dispose();
        time.Advance(TimeSpan.FromMinutes(5));

        renewedBeforeDisposal.ShouldBe(1);
        lease.Renewals.ShouldBe(renewedBeforeDisposal);
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        var lease = Lease();
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);

        renewal.Dispose();

        Should.NotThrow(renewal.Dispose);
    }

    [Fact]
    public void Dispose_WhenTheRenewalStops_NeverReleasesTheLease()
    {
        // The attempt that borrowed the lease owns its release; stopping renewal must not revoke ownership.
        var lease = Lease();
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);

        renewal.Dispose();

        lease.Disposed.ShouldBeFalse();
    }

    [Fact]
    public void Lost_WhenEveryRenewalSucceeds_RemainsFalse()
    {
        var lease = Lease();
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        using var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);

        time.Advance(TimeSpan.FromSeconds(60));

        lease.Renewals.ShouldBe(3);
        renewal.Lost.ShouldBeFalse();
    }

    [Fact]
    public void Lost_WhenOwnershipPassedToAnotherWorker_BecomesTrue()
    {
        var lease = Lease(reportLost: true);
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        using var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);

        time.Advance(TimeSpan.FromSeconds(20));

        renewal.Lost.ShouldBeTrue();
    }

    [Fact]
    public void Lost_WhenRenewalThrows_BecomesTrueWithoutEscapingTheTimerCallback()
    {
        // An escaping exception on a timer callback would terminate the process.
        var lease = Lease(failRenewal: true);
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        using var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), NullLogger.Instance);

        Should.NotThrow(() => time.Advance(TimeSpan.FromSeconds(20)));

        renewal.Lost.ShouldBeTrue();
    }

    [Fact]
    public void Lost_WhenTheLoggerFails_StillReflectsTheRenewalOutcome()
    {
        var lease = Lease(reportLost: true);
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        var logger = new RecordingLogger<ExecutionLeaseRenewalTests> { ThrowOnWrite = true };
        using var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), logger);

        Should.NotThrow(() => time.Advance(TimeSpan.FromSeconds(20)));

        renewal.Lost.ShouldBeTrue();
    }

    [Fact]
    public void Lost_WhenRenewalSucceeds_EmitsOnlyContentFreeDiagnostics()
    {
        var lease = Lease();
        var time = new FakeTimeProvider(DurableJournalTestData.Now);
        var logger = new RecordingLogger<ExecutionLeaseRenewalTests>();
        using var renewal = new ExecutionLeaseRenewal(lease, time, TimeSpan.FromSeconds(20), logger);

        time.Advance(TimeSpan.FromSeconds(20));

        var entries = logger.Snapshot();
        entries.Length.ShouldBe(1);
        entries[0].EventId.Id.ShouldBeInRange(26000, 26009);
        entries[0].Message.ShouldNotContain(DurableJournalTestData.SessionId.Value.ToString());
    }

    private static TestExecutionLease Lease(bool reportLost = false, bool failRenewal = false) =>
        new(DurableJournalTestData.Address(), new FencingToken(1))
        {
            ReportLost = reportLost,
            FailRenewal = failRenewal,
        };
}
