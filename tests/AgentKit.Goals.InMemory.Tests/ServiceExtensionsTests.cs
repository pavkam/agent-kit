// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.InMemory.Tests;

/// <summary>Verifies keyed in-memory goal-store registration.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddInMemoryGoalStore_WhenServicesIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddInMemoryGoalStore(new GoalStoreKey("k"))).ParamName.ShouldBe("services");

    [Fact]
    public void AddInMemoryGoalStore_WhenKeyIsDefault_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new ServiceCollection().AddInMemoryGoalStore(default)).ParamName.ShouldBe("key");

    [Fact]
    public void AddInMemoryGoalStore_WhenResolved_ReturnsOneSingletonPerKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddInMemoryGoalStore(new GoalStoreKey("a"));
        _ = services.AddInMemoryGoalStore(new GoalStoreKey("b"));
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredKeyedService<IGoalStore>("a");

        provider.GetRequiredKeyedService<IGoalStore>("a").ShouldBeSameAs(first);
        provider.GetRequiredKeyedService<IGoalStore>("b").ShouldNotBeSameAs(first);
        first.ShouldBeOfType<InMemoryGoalStore>().Descriptor.IsDurable.ShouldBeFalse();
    }

    [Fact]
    public void AddInMemoryGoalStore_WhenTheSameKeyIsRegisteredTwice_KeepsTheFirstRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemoryGoalStore(new GoalStoreKey("a"));
        _ = services.AddInMemoryGoalStore(new GoalStoreKey("a"));

        services.Count(static descriptor => descriptor.ServiceType == typeof(IGoalStore)).ShouldBe(1);
    }

    [Fact]
    public async Task AddInMemoryGoalStore_WhenConfigured_AppliesTheScannerOptions()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddInMemoryGoalStore(new GoalStoreKey("a"), options => options.AuthorizedIntentScanners.Add(new ComponentId("worker")));
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredKeyedService<IGoalStore>("a");

        var result = await store.ReadIntentsAsync(new GoalIntentScanRequest(new ComponentId("worker"), 0, 5), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<GoalPage>().Items.ShouldBeEmpty();
    }
}
