// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies the SQLite lease manager's guards, database-allocated generations, and cross-instance exclusion.</summary>
public sealed class SqliteDurableLeaseManagerTests
{
    /// <summary>Verifies the shared database is required, because the manager owns no target of its own.</summary>
    [Fact]
    public void Constructor_WhenDatabaseIsNull_ThrowsForTheDatabaseArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableLeaseManager(
            null!, new TestLeaseIdGenerator(), new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("database");
    }

    /// <summary>Verifies a lease-identity generator is required rather than defaulted.</summary>
    [Fact]
    public void Constructor_WhenLeaseIdsAreNull_ThrowsForTheLeaseIdsArgument()
    {
        var store = new SqliteDurableTestStore();

        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableLeaseManager(
            new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault()),
            null!,
            new FakeTimeProvider(DurabilityConformanceData.Now)));

        exception.ParamName.ShouldBe("leaseIds");
    }

    /// <summary>Verifies an injected clock is required, so expiry never depends on an ambient clock.</summary>
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsForTheTimeProviderArgument()
    {
        var store = new SqliteDurableTestStore();

        var exception = Should.Throw<ArgumentNullException>(() => new SqliteDurableLeaseManager(
            new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault()),
            new TestLeaseIdGenerator(),
            null!));

        exception.ParamName.ShouldBe("timeProvider");
    }

    /// <summary>Verifies a null request is refused before the database is touched.</summary>
    [Fact]
    public async Task AcquireAsync_WhenRequestIsNull_ThrowsForTheRequestArgument()
    {
        var store = new SqliteDurableTestStore();
        var manager = CreateManager(store, out _);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => manager.AcquireAsync(null!, TestContext.Current.CancellationToken).AsTask());

        exception.ParamName.ShouldBe("request");
    }

    /// <summary>Verifies acquisition before trusted bootstrap fails closed instead of creating a store implicitly.</summary>
    [Fact]
    public async Task AcquireAsync_WhenBootstrapDidNotRun_ThrowsAnOpenFailure()
    {
        var store = new SqliteDurableTestStore();
        var manager = CreateManager(store, out _);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken).AsTask());

        exception.Data["agentkit.failure_kind"].ShouldBe("open_failed");
    }

    /// <summary>Verifies a generator that produces a default identity is refused rather than persisted.</summary>
    [Fact]
    public async Task AcquireAsync_WhenTheGeneratorProducesADefaultIdentity_Throws()
    {
        var store = new SqliteDurableTestStore();
        var manager = new SqliteDurableLeaseManager(
            store.CreateDatabase(),
            new DefaultLeaseIdGenerator(),
            new FakeTimeProvider(DurabilityConformanceData.Now));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies the database, not the process, allocates generations, so a restart cannot reuse one.</summary>
    /// <remarks>
    /// A process-local counter would restart at one after a crash and let a stale writer look current. Allocating the
    /// token inside the store is the property that makes takeover safe across processes.
    /// </remarks>
    [Fact]
    public async Task AcquireAsync_WhenAFreshManagerTakesOver_AllocatesAStrictlyGreaterGeneration()
    {
        var store = new SqliteDurableTestStore();
        var first = CreateManager(store, out var firstClock);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        var original = (await first.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        firstClock.Advance(TimeSpan.FromMinutes(5));

        var second = CreateManager(store, out var secondClock);
        secondClock.Advance(TimeSpan.FromMinutes(5));
        await second.InitializeAsync(TestContext.Current.CancellationToken);
        var takeover = await second.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);

        var acquired = takeover.ShouldBeOfType<ExecutionLeaseAcquired>();
        acquired.Lease.FencingToken.Value.ShouldBeGreaterThan(original.FencingToken.Value);
    }

    /// <summary>Verifies a second manager over the same database is refused while the first lease is live.</summary>
    [Fact]
    public async Task AcquireAsync_WhenAnotherManagerHoldsALiveLease_ReportsTheCurrentOwner()
    {
        var store = new SqliteDurableTestStore();
        var first = CreateManager(store, out _);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        _ = (await first.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ExecutionLeaseAcquired>();

        var second = CreateManager(store, out _);
        await second.InitializeAsync(TestContext.Current.CancellationToken);
        var contended = await second.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);

        contended.ShouldBeOfType<ExecutionLeaseHeldByAnotherWorker>().CurrentOwnerWorkerId
            .ShouldBe(DurabilityConformanceData.Worker);
    }

    /// <summary>Verifies disposal releases ownership so another manager acquires without waiting for expiry.</summary>
    [Fact]
    public async Task DisposeAsync_WhenLeaseIsCurrent_ReleasesOwnershipForAnotherManager()
    {
        var store = new SqliteDurableTestStore();
        var first = CreateManager(store, out _);
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        var lease = (await first.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        await lease.DisposeAsync();

        var second = CreateManager(store, out _);
        await second.InitializeAsync(TestContext.Current.CancellationToken);
        var acquired = await second.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(DurabilityConformanceData.OtherWorker),
            TestContext.Current.CancellationToken);

        _ = acquired.ShouldBeOfType<ExecutionLeaseAcquired>();
    }

    /// <summary>Verifies renewing a disposed lease throws instead of silently reacquiring ownership.</summary>
    [Fact]
    public async Task RenewAsync_WhenLeaseWasDisposed_ThrowsObjectDisposed()
    {
        var store = new SqliteDurableTestStore();
        var manager = CreateManager(store, out _);
        await manager.InitializeAsync(TestContext.Current.CancellationToken);
        var lease = (await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ExecutionLeaseAcquired>().Lease;
        await lease.DisposeAsync();

        _ = await Should.ThrowAsync<ObjectDisposedException>(
            () => lease.RenewAsync(TestContext.Current.CancellationToken).AsTask());
    }

    /// <summary>Verifies a renewal after the persisted expiry passed reports lost ownership rather than extending it.</summary>
    [Fact]
    public async Task RenewAsync_WhenThePersistedExpiryHasPassed_ReportsLostOwnership()
    {
        var store = new SqliteDurableTestStore();
        var manager = CreateManager(store, out var clock);
        await manager.InitializeAsync(TestContext.Current.CancellationToken);
        var lease = (await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken))
            .ShouldBeOfType<ExecutionLeaseAcquired>().Lease;

        clock.Advance(TimeSpan.FromMinutes(5));
        var renewal = await lease.RenewAsync(TestContext.Current.CancellationToken);

        _ = renewal.ShouldBeOfType<LeaseLost>();
    }

    private static SqliteDurableLeaseManager CreateManager(SqliteDurableTestStore store, out FakeTimeProvider clock)
    {
        clock = new FakeTimeProvider(DurabilityConformanceData.Now);
        return new SqliteDurableLeaseManager(
            new SqliteDurableDatabase(store.Target, SqliteDurableStoreSettings.CreateDefault()),
            new TestLeaseIdGenerator(),
            clock);
    }

    /// <summary>Produces distinct lease identities for these scenarios.</summary>
    private sealed class TestLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
    {
        public ExecutionLeaseId Create() => new(Guid.NewGuid());
    }

    /// <summary>Models a misconfigured generator that cannot produce a usable identity.</summary>
    private sealed class DefaultLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
    {
        public ExecutionLeaseId Create() => default;
    }
}
