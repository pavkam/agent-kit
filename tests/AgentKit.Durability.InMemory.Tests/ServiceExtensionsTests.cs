// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly DurableLeaseManagerKey LeaseKey = new("leases");
    private static readonly DurableLeaseManagerKey OtherLeaseKey = new("other-leases");
    private static readonly DurableJournalKey OtherJournalKey = new("other-journal");

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddInMemoryDurableLeaseManager(LeaseKey))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenTheKeyIsDefault_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInMemoryDurableLeaseManager(default))
            .ParamName.ShouldBe("key");

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenCalledTwiceWithTheSameKey_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemoryDurableLeaseManager(LeaseKey).AddInMemoryDurableLeaseManager(LeaseKey);

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableLeaseManager)
                && Equals(descriptor.ServiceKey, LeaseKey.Value))
            .ShouldBe(1);
    }

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenCalledWithDistinctKeys_RegistersOnePerKey()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemoryDurableLeaseManager(LeaseKey).AddInMemoryDurableLeaseManager(OtherLeaseKey);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IDurableLeaseManager>(LeaseKey.Value)
            .ShouldNotBeSameAs(provider.GetRequiredKeyedService<IDurableLeaseManager>(OtherLeaseKey.Value));
    }

    [Fact]
    public async Task AddInMemoryDurableLeaseManager_WhenResolvedByKey_GrantsOwnership()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemoryDurableLeaseManager(LeaseKey);
        using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredKeyedService<IDurableLeaseManager>(LeaseKey.Value);

        var address = new DurableOperationAddress(
            new AgentId(Guid.Parse("10000000-0000-0000-0000-000000000009")),
            new SessionId(Guid.Parse("20000000-0000-0000-0000-000000000009")),
            new RunId(Guid.Parse("30000000-0000-0000-0000-000000000009")),
            new OperationId(Guid.Parse("40000000-0000-0000-0000-000000000009")));
        var result = await manager.AcquireAsync(
            new ExecutionLeaseRequest(address, new WorkerId(Guid.Parse("50000000-0000-0000-0000-000000000009")), TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);

        var acquired = result.ShouldBeOfType<ExecutionLeaseAcquired>();
        await acquired.Lease.DisposeAsync();
    }

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenAnUnkeyedManagerIsAlreadyRegistered_PreservesItAlongsideTheKeyedDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDurableLeaseManager>(new InMemoryDurableLeaseManager(
            new GuidExecutionLeaseIdGenerator(), TimeProvider.System));

        _ = services.AddInMemoryDurableLeaseManager(LeaseKey);

        services.Count(descriptor => descriptor.ServiceType == typeof(IDurableLeaseManager)).ShouldBe(2);
    }

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenClockIsAlreadyRegistered_PreservesIt()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddInMemoryDurableLeaseManager(LeaseKey);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }

    [Fact]
    public void AddInMemoryDurableOperationJournal_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() =>
                ((IServiceCollection) null!).AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey))
            .ParamName.ShouldBe("services");

    [Fact]
    public void AddInMemoryDurableOperationJournal_WhenTheKeyIsDefault_ThrowsArgumentNullExceptionWithParamName() =>
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInMemoryDurableOperationJournal(default))
            .ParamName.ShouldBe("key");

    [Fact]
    public void AddInMemoryDurableOperationJournal_WhenCalledTwiceWithTheSameKey_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();

        _ = services
            .AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey)
            .AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey);

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableOperationJournal)
                && Equals(descriptor.ServiceKey, DurableJournalTestData.JournalKey.Value))
            .ShouldBe(1);
    }

    [Fact]
    public void AddInMemoryDurableOperationJournal_WhenCalledWithDistinctKeys_RegistersOnePerKey()
    {
        var services = new ServiceCollection();
        _ = AddSecurity(services);

        _ = services
            .AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey)
            .AddInMemoryDurableOperationJournal(OtherJournalKey);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IDurableOperationJournal>(DurableJournalTestData.JournalKey.Value)
            .ShouldBeOfType<InMemoryDurableOperationJournal>().Key.ShouldBe(DurableJournalTestData.JournalKey);
        provider.GetRequiredKeyedService<IDurableOperationJournal>(OtherJournalKey.Value)
            .ShouldBeOfType<InMemoryDurableOperationJournal>().Key.ShouldBe(OtherJournalKey);
    }

    [Fact]
    public async Task AddInMemoryDurableOperationJournal_WhenResolvedByKey_RecordsAcceptance()
    {
        var services = new ServiceCollection();
        var harness = AddSecurity(services);
        _ = services.AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey);
        using var provider = services.BuildServiceProvider();
        var journal = provider
            .GetRequiredKeyedService<IDurableOperationJournal>(DurableJournalTestData.JournalKey.Value)
            .ShouldBeOfType<InMemoryDurableOperationJournal>();
        var start = DurableJournalTestData.Start(new FencingToken(1));

        var result = await journal.RecordStartAsync(
            harness.Authorize(
                start,
                DurableJournalTestData.JournalKey,
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
    public void AddInMemoryDurableOperationJournal_WhenAnUnkeyedJournalIsAlreadyRegistered_PreservesItAlongsideTheKeyedDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDurableOperationJournal>(new DurableJournalFixture().Journal);

        _ = services.AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey);

        services.Count(descriptor => descriptor.ServiceType == typeof(IDurableOperationJournal)).ShouldBe(2);
    }

    [Fact]
    public void AddInMemoryDurableOperationJournal_WhenClockIsAlreadyRegistered_PreservesIt()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddInMemoryDurableOperationJournal(DurableJournalTestData.JournalKey);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }

    private static TestDurableSecurityHarness AddSecurity(IServiceCollection services)
    {
        var harness = new TestDurableSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(harness);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(harness);
        return harness;
    }
}
