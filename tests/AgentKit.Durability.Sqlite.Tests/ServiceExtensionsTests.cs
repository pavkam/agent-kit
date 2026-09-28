// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly DurableBackendKey BackendKey = new("sqlite-backend");
    private static readonly DurableLeaseManagerKey LeaseKey = new("sqlite-leases");
    private static readonly DurableLeaseManagerKey OtherLeaseKey = new("other-sqlite-leases");
    private static readonly DurableJournalKey JournalKey = new("sqlite-journal");
    private static readonly DurableJournalKey OtherJournalKey = new("other-sqlite-journal");

    [Fact]
    public void AddSqliteDurableExecutionBackend_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
                () => ((IServiceCollection) null!).AddSqliteDurableExecutionBackend(BackendKey))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddSqliteDurableExecutionBackend_WhenTheKeyIsDefault_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteDurableExecutionBackend(default))
            .ParamName.ShouldBe("key");

    [Fact]
    public void AddSqliteDurableExecutionBackend_WhenResolvedByKey_ClaimsHostLocalOwnershipOnly()
    {
        var services = new ServiceCollection();

        _ = services.AddSqliteDurableExecutionBackend(BackendKey);

        using var provider = services.BuildServiceProvider();
        var descriptor = provider.GetRequiredKeyedService<IDurableExecutionBackend>(BackendKey.Value).Descriptor;
        descriptor.Capabilities.SupportsDistributedOwnership.ShouldBeFalse();
        descriptor.Capabilities.SupportsExternalHandoff.ShouldBeFalse();
        descriptor.Capabilities.SupportsReconciliation.ShouldBeFalse();
        descriptor.SupportsFencing.ShouldBeTrue();
    }

    [Fact]
    public void AddSqliteDurableLeaseManager_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddSqliteDurableLeaseManager(
                LeaseKey, CreateDatabase()))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddSqliteDurableLeaseManager_WhenTheDatabaseIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
                () => new ServiceCollection().AddSqliteDurableLeaseManager(LeaseKey, null!))
            .ParamName.ShouldBe("database");

    [Fact]
    public void AddSqliteDurableLeaseManager_WhenTheKeyIsDefault_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
                () => new ServiceCollection().AddSqliteDurableLeaseManager(default, CreateDatabase()))
            .ParamName.ShouldBe("key");

    [Fact]
    public void AddSqliteDurableLeaseManager_WhenCalledTwiceWithTheSameKey_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();
        var database = CreateDatabase();

        _ = services
            .AddSqliteDurableLeaseManager(LeaseKey, database)
            .AddSqliteDurableLeaseManager(LeaseKey, database);

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableLeaseManager)
                && Equals(descriptor.ServiceKey, LeaseKey.Value))
            .ShouldBe(1);
    }

    [Fact]
    public void AddSqliteDurableLeaseManager_WhenCalledWithDistinctKeys_RegistersOnePerKey()
    {
        var services = new ServiceCollection();
        var database = CreateDatabase();

        _ = services
            .AddSqliteDurableLeaseManager(LeaseKey, database)
            .AddSqliteDurableLeaseManager(OtherLeaseKey, database);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IDurableLeaseManager>(LeaseKey.Value)
            .ShouldNotBeSameAs(provider.GetRequiredKeyedService<IDurableLeaseManager>(OtherLeaseKey.Value));
    }

    [Fact]
    public async Task AddSqliteDurableLeaseManager_WhenResolvedByKey_GrantsOwnership()
    {
        var services = new ServiceCollection();
        var store = new SqliteDurableTestStore();
        _ = services.AddSqliteDurableLeaseManager(LeaseKey, store.CreateDatabase());
        using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredKeyedService<IDurableLeaseManager>(LeaseKey.Value);

        var result = await manager.AcquireAsync(
            DurabilityConformanceData.LeaseRequest(), TestContext.Current.CancellationToken);

        var acquired = result.ShouldBeOfType<ExecutionLeaseAcquired>();
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public void AddSqliteDurableLeaseManager_WhenClockIsAlreadyRegistered_PreservesIt()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddSqliteDurableLeaseManager(LeaseKey, CreateDatabase());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddSqliteDurableOperationJournal(
                JournalKey, CreateDatabase()))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenTheDatabaseIsNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
                () => new ServiceCollection().AddSqliteDurableOperationJournal(JournalKey, null!))
            .ParamName.ShouldBe("database");

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenTheKeyIsDefault_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(
                () => new ServiceCollection().AddSqliteDurableOperationJournal(default, CreateDatabase()))
            .ParamName.ShouldBe("key");

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenCalledTwiceWithTheSameKey_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();
        var database = CreateDatabase();

        _ = services
            .AddSqliteDurableOperationJournal(JournalKey, database)
            .AddSqliteDurableOperationJournal(JournalKey, database);

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableOperationJournal)
                && Equals(descriptor.ServiceKey, JournalKey.Value))
            .ShouldBe(1);
    }

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenCalledWithDistinctKeys_RegistersOnePerKey()
    {
        var services = new ServiceCollection();
        _ = AddSecurity(services);
        var database = CreateDatabase();

        _ = services
            .AddSqliteDurableOperationJournal(JournalKey, database)
            .AddSqliteDurableOperationJournal(OtherJournalKey, database);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IDurableOperationJournal>(JournalKey.Value)
            .ShouldBeOfType<SqliteDurableOperationJournal>().Key.ShouldBe(JournalKey);
        provider.GetRequiredKeyedService<IDurableOperationJournal>(OtherJournalKey.Value)
            .ShouldBeOfType<SqliteDurableOperationJournal>().Key.ShouldBe(OtherJournalKey);
    }

    [Fact]
    public async Task AddSqliteDurableOperationJournal_WhenResolvedByKey_RecordsAcceptance()
    {
        var services = new ServiceCollection();
        var harness = AddSecurity(services);
        var store = new SqliteDurableTestStore();
        _ = services.AddSqliteDurableOperationJournal(JournalKey, store.CreateDatabase());
        using var provider = services.BuildServiceProvider();
        var journal = provider.GetRequiredKeyedService<IDurableOperationJournal>(JournalKey.Value)
            .ShouldBeOfType<SqliteDurableOperationJournal>();
        var start = DurabilityConformanceData.Start(JournalKey, new FencingToken(1));

        var result = await journal.RecordStartAsync(
            harness.Authorize(
                start,
                JournalKey,
                journal.SecurityAudience,
                start.Descriptor.Binding.Address,
                start.Descriptor.Binding.ExecutionContext.Authorization,
                DurableJournalSecurityBinding.Fingerprint(start),
                SecurityOperationKind.StateMutation,
                SecurityEffect.Create,
                new FencingToken(1)),
            TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<DurableRecorded>();
    }

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenClockIsAlreadyRegistered_PreservesIt()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddSqliteDurableOperationJournal(JournalKey, CreateDatabase());
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }

    [Fact]
    public void AddSqliteDurableOperationJournal_WhenSecurityIsMissing_FailsToResolveRatherThanSkippingEnforcement()
    {
        var services = new ServiceCollection();
        _ = services.AddSqliteDurableOperationJournal(JournalKey, CreateDatabase());
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<InvalidOperationException>(
            () => provider.GetRequiredKeyedService<IDurableOperationJournal>(JournalKey.Value));
    }

    private static SqliteDurableDatabase CreateDatabase() =>
        new(new SqliteDurableTestStore().Target, SqliteDurableStoreSettings.CreateDefault());

    private static TestDurableSecurityHarness AddSecurity(IServiceCollection services)
    {
        var harness = new TestDurableSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(harness);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(harness);
        return harness;
    }
}
