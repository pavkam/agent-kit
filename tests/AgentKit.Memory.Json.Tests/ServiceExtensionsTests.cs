// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Verifies the JSON registration extensions.</summary>
public sealed class ServiceExtensionsTests: IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();

    /// <inheritdoc/>
    public void Dispose() => _root.Dispose();

    [Fact]
    public void Registrations_WhenArgumentsAreNull_ThrowNamingThem()
    {
        var target = _root.Target();

        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddJsonMemoryStore(new MemoryStoreKey("k"), target)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonMemoryStore(new MemoryStoreKey("k"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonMemoryStore(default, target)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonDocumentStore(new DocumentStoreKey("k"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonDocumentStore(default, target)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonVectorIndex(null!, target)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddJsonVectorIndex(MemoryTestData.Space(), null!)).ParamName.ShouldBe("target");
    }

    [Fact]
    public void Registrations_WhenABoundIsNotPositive_ThrowArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddJsonMemoryStore(new MemoryStoreKey("k"), _root.Target(), options => options.MaximumRecordBytes = 0)).ParamName.ShouldBe("maximumRecordBytes");

    [Fact]
    public async Task Registrations_WhenResolved_ReturnDurableSingletonsPerKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddJsonMemoryStore(new MemoryStoreKey("m"), _root.Target("m"));
        _ = services.AddJsonDocumentStore(new DocumentStoreKey("d"), _root.Target("d"));
        _ = services.AddJsonVectorIndex(MemoryTestData.Space("v"), _root.Target("v"));
        using var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredKeyedService<IMemoryStore>("m");
        var documents = provider.GetRequiredKeyedService<IDocumentStore>("d");
        var vectors = provider.GetRequiredKeyedService<IVectorIndex>("v");

        provider.GetRequiredKeyedService<IMemoryStore>("m").ShouldBeSameAs(memory);
        memory.Descriptor.IsDurable.ShouldBeTrue();
        documents.Descriptor.Key.ShouldBe(new DocumentStoreKey("d"));
        vectors.IsDurable.ShouldBeTrue();
        await ((JsonMemoryStore) memory).InitializeAsync(TestContext.Current.CancellationToken);
        await ((JsonDocumentStore) documents).InitializeAsync(TestContext.Current.CancellationToken);
        await ((JsonVectorIndex) vectors).InitializeAsync(TestContext.Current.CancellationToken);
        ((JsonMemoryStore) memory).Dispose();
        ((JsonDocumentStore) documents).Dispose();
        ((JsonVectorIndex) vectors).Dispose();
    }

    [Fact]
    public void Registrations_WhenTheSameKeyIsRegisteredTwice_KeepTheFirstRegistration()
    {
        var services = new ServiceCollection();
        var target = _root.Target();
        _ = services.AddJsonMemoryStore(new MemoryStoreKey("a"), target);
        _ = services.AddJsonMemoryStore(new MemoryStoreKey("a"), target);
        _ = services.AddJsonDocumentStore(new DocumentStoreKey("a"), target);
        _ = services.AddJsonDocumentStore(new DocumentStoreKey("a"), target);
        _ = services.AddJsonVectorIndex(MemoryTestData.Space("a"), target);
        _ = services.AddJsonVectorIndex(MemoryTestData.Space("a"), target);

        services.Count(static descriptor => descriptor.ServiceType == typeof(IMemoryStore)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IDocumentStore)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IVectorIndex)).ShouldBe(1);
    }
}
