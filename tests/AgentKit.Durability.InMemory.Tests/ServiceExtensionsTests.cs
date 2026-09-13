// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenServicesAreNull_ThrowsArgumentNullExceptionWithParamName()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(services.AddInMemoryDurableLeaseManager);
        exception.ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenCalledTwice_RegistersOneReplaceableDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemoryDurableLeaseManager().AddInMemoryDurableLeaseManager();

        services.Count(descriptor =>
                descriptor.ServiceType == typeof(IDurableLeaseManager)
                && descriptor.ImplementationType == typeof(InMemoryDurableLeaseManager))
            .ShouldBe(1);
    }

    [Fact]
    public async Task AddInMemoryDurableLeaseManager_WhenResolved_GrantsOwnership()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemoryDurableLeaseManager();
        using var provider = services.BuildServiceProvider();
        var manager = provider.GetRequiredService<IDurableLeaseManager>();

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
    public void AddInMemoryDurableLeaseManager_WhenACustomManagerIsAlreadyRegistered_PreservesItAlongsideTheDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IDurableLeaseManager>(new InMemoryDurableLeaseManager(
            new GuidExecutionLeaseIdGenerator(), TimeProvider.System));

        _ = services.AddInMemoryDurableLeaseManager();

        services.Count(descriptor => descriptor.ServiceType == typeof(IDurableLeaseManager)).ShouldBe(2);
    }

    [Fact]
    public void AddInMemoryDurableLeaseManager_WhenClockIsAlreadyRegistered_PreservesIt()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddInMemoryDurableLeaseManager();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }
}
