// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddInMemorySessionStore_WhenCalled_RegistersOneValidStoreGraph()
    {
        var services = new ServiceCollection();
        var security = new TestSecurityHarness();

        _ = services.AddSingleton<ISecurityGrantStore>(security);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(security);
        _ = services.AddInMemorySessionStore();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        _ = provider.GetRequiredService<ISessionStore>().ShouldBeOfType<InMemorySessionStore>();
        provider.GetServices<ISessionStore>().Count().ShouldBe(1);
        _ = provider.GetRequiredService<IIdentifierGenerator<BranchId>>();
        _ = provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>();
        _ = provider.GetRequiredService<TimeProvider>();
        provider.GetRequiredService<IOptions<InMemorySessionStoreOptions>>().Value.MaximumIssuedReadSnapshots.ShouldBe(4096);
    }

    [Fact]
    public void AddInMemorySessionStore_WhenServicesIsNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var exception = Should.Throw<ArgumentNullException>(() => services.AddInMemorySessionStore());

        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddInMemorySessionStore_WhenCalledTwice_KeepsOneStoreRegistration()
    {
        var services = new ServiceCollection();
        var security = new TestSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(security);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(security);

        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionStore();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetServices<ISessionStore>().ShouldHaveSingleItem();
        provider.GetRequiredService<ISessionStore>().ShouldBeSameAs(provider.GetRequiredService<ISessionStore>());
    }

    [Fact]
    public void AddInMemorySessionStore_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();
        var security = new TestSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(security);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(security);

        _ = services.AddInMemorySessionStore(static options => options.MaximumIssuedReadSnapshots = 7);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<InMemorySessionStoreOptions>>().Value.MaximumIssuedReadSnapshots.ShouldBe(7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddInMemorySessionStore_WhenMaximumIssuedReadSnapshotsIsNotPositive_FailsValidationOnAccess(int bound)
    {
        var services = new ServiceCollection();
        var security = new TestSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(security);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(security);
        _ = services.AddInMemorySessionStore(options => options.MaximumIssuedReadSnapshots = bound);
        using var provider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<InMemorySessionStoreOptions>>().Value);

        exception.Failures.ShouldHaveSingleItem().ShouldBe("MaximumIssuedReadSnapshots must be positive.");
        _ = Should.Throw<OptionsValidationException>(provider.GetRequiredService<ISessionStore>);
    }

    [Fact]
    public async Task AddInMemorySessionStore_WhenConfigureBoundsSnapshots_ResolvedStoreHonorsTheBound()
    {
        var services = new ServiceCollection();
        var security = new TestSecurityHarness();
        _ = services.AddSingleton<ISecurityGrantStore>(security);
        _ = services.AddSingleton<ISecurityAuditDispatcher>(security);
        _ = services.AddInMemorySessionStore(static options => options.MaximumIssuedReadSnapshots = 1);
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ISessionStore>().ShouldBeOfType<InMemorySessionStore>();
        TestSecurityHarness.Register(store, security);
        var descriptor = await TestFactory.CreateSessionAsync(store);
        var context = TestFactory.OperationContext(descriptor.Address);
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(0), new IdempotencyKey("first"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 1, "one")]), TestContext.Current.CancellationToken);
        var firstPage = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(new SessionAppendRequest(context, descriptor.ActiveBranchId, new SessionVersion(1), new IdempotencyKey("second"), [TestFactory.MessageEntry(descriptor.Address, descriptor.ActiveBranchId, 2, "two")]), TestContext.Current.CancellationToken);
        _ = (SessionPage) await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, new SessionSequence(0), 10), TestContext.Current.CancellationToken);

        var continued = await store.ReadAsync(new SessionReadRequest(context, descriptor.ActiveBranchId, firstPage.ThroughSequence, 10, firstPage.Snapshot!), TestContext.Current.CancellationToken);

        _ = continued.ShouldBeOfType<SessionReadFailed>();
    }

    [Fact]
    public void AddInMemorySessionStore_WhenCalledTwice_RegistersOneStore()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionStore();

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionStore)).ShouldBe(1);
    }
}
