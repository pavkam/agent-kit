// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory.Tests;

/// <summary>Verifies the in-memory registration extensions.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void Registrations_WhenServicesIsNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddInMemoryMemoryStore(new MemoryStoreKey("k"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddInMemoryDocumentStore(new DocumentStoreKey("k"))).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddInMemoryVectorIndex(MemoryTestData.Space())).ParamName.ShouldBe("services");
    }

    [Fact]
    public void Registrations_WhenKeyOrSpaceIsInvalid_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInMemoryMemoryStore(default)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInMemoryDocumentStore(default)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddInMemoryVectorIndex(null!)).ParamName.ShouldBe("space");
    }

    [Fact]
    public void AddInMemoryMemoryStore_WhenResolved_ReturnsOneSingletonPerKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddInMemoryMemoryStore(new MemoryStoreKey("a"));
        _ = services.AddInMemoryMemoryStore(new MemoryStoreKey("b"));
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredKeyedService<IMemoryStore>("a");

        provider.GetRequiredKeyedService<IMemoryStore>("a").ShouldBeSameAs(first);
        provider.GetRequiredKeyedService<IMemoryStore>("b").ShouldNotBeSameAs(first);
        first.Descriptor.Key.ShouldBe(new MemoryStoreKey("a"));
        first.Descriptor.IsDurable.ShouldBeFalse();
    }

    [Fact]
    public void AddInMemoryDocumentStore_WhenResolved_ReturnsOneSingletonPerKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddInMemoryDocumentStore(new DocumentStoreKey("a"));
        using var provider = services.BuildServiceProvider();

        var store = provider.GetRequiredKeyedService<IDocumentStore>("a");

        provider.GetRequiredKeyedService<IDocumentStore>("a").ShouldBeSameAs(store);
        store.Descriptor.Key.ShouldBe(new DocumentStoreKey("a"));
    }

    [Fact]
    public void AddInMemoryVectorIndex_WhenResolved_ReturnsTheIndexUnderItsSpaceKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddInMemoryVectorIndex(MemoryTestData.Space("idx"));
        using var provider = services.BuildServiceProvider();

        var index = provider.GetRequiredKeyedService<IVectorIndex>("idx");

        index.VectorSpace.IndexKey.ShouldBe(new VectorIndexKey("idx"));
        index.ApproximateSearch.ShouldBeFalse();
    }

    [Fact]
    public void Registrations_WhenTheSameKeyIsRegisteredTwice_KeepTheFirstRegistration()
    {
        var services = new ServiceCollection();
        _ = services.AddInMemoryMemoryStore(new MemoryStoreKey("a"));
        _ = services.AddInMemoryMemoryStore(new MemoryStoreKey("a"));
        _ = services.AddInMemoryDocumentStore(new DocumentStoreKey("a"));
        _ = services.AddInMemoryDocumentStore(new DocumentStoreKey("a"));
        _ = services.AddInMemoryVectorIndex(MemoryTestData.Space("a"));
        _ = services.AddInMemoryVectorIndex(MemoryTestData.Space("a"));

        services.Count(static descriptor => descriptor.ServiceType == typeof(IMemoryStore)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IDocumentStore)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IVectorIndex)).ShouldBe(1);
    }

    [Fact]
    public void Registrations_WhenTheBaseIsRegistered_InstallNoStoreImplicitly()
    {
        var services = new ServiceCollection();

        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(IMemoryStore) || descriptor.ServiceType == typeof(IDocumentStore) || descriptor.ServiceType == typeof(IVectorIndex));
    }
}
