// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Verifies the SQLite registration extensions.</summary>
public sealed class ServiceExtensionsTests: IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    [Fact]
    public void Registrations_WhenArgumentsAreNull_ThrowNamingThem()
    {
        var target = _database.Target();

        Should.Throw<ArgumentNullException>(() => ((IServiceCollection) null!).AddSqliteMemoryStore(new MemoryStoreKey("k"), target)).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteMemoryStore(new MemoryStoreKey("k"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteMemoryStore(default, target)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteDocumentStore(new DocumentStoreKey("k"), null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteDocumentStore(default, target)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteVectorIndex(null!, target)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddSqliteVectorIndex(MemoryTestData.Space(), null!)).ParamName.ShouldBe("target");
    }

    [Fact]
    public void Registrations_WhenABoundIsInvalid_ThrowArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ServiceCollection().AddSqliteMemoryStore(new MemoryStoreKey("k"), _database.Target(), options => options.MaximumRecordBytes = 0)).ParamName.ShouldBe("maximumRecordBytes");

    [Fact]
    public async Task Registrations_WhenResolved_ReturnDurableSingletonsPerKey()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(new TestGoalGrants());
        _ = services.AddSqliteMemoryStore(new MemoryStoreKey("m"), _database.Target("m"));
        _ = services.AddSqliteDocumentStore(new DocumentStoreKey("d"), _database.Target("d"));
        _ = services.AddSqliteVectorIndex(MemoryTestData.Space("v"), _database.Target("v"));
        using var provider = services.BuildServiceProvider();

        var memory = provider.GetRequiredKeyedService<IMemoryStore>("m");
        var documents = provider.GetRequiredKeyedService<IDocumentStore>("d");
        var vectors = provider.GetRequiredKeyedService<IVectorIndex>("v");

        provider.GetRequiredKeyedService<IMemoryStore>("m").ShouldBeSameAs(memory);
        memory.Descriptor.IsDurable.ShouldBeTrue();
        documents.Descriptor.Key.ShouldBe(new DocumentStoreKey("d"));
        vectors.ApproximateSearch.ShouldBeFalse();
        await ((SqliteMemoryStore) memory).InitializeAsync(TestContext.Current.CancellationToken);
        await ((SqliteDocumentStore) documents).InitializeAsync(TestContext.Current.CancellationToken);
        await ((SqliteVectorIndex) vectors).InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Registrations_WhenTheSameKeyIsRegisteredTwice_KeepTheFirstRegistration()
    {
        var services = new ServiceCollection();
        var target = _database.Target();
        _ = services.AddSqliteMemoryStore(new MemoryStoreKey("a"), target);
        _ = services.AddSqliteMemoryStore(new MemoryStoreKey("a"), target);
        _ = services.AddSqliteDocumentStore(new DocumentStoreKey("a"), target);
        _ = services.AddSqliteDocumentStore(new DocumentStoreKey("a"), target);
        _ = services.AddSqliteVectorIndex(MemoryTestData.Space("a"), target);
        _ = services.AddSqliteVectorIndex(MemoryTestData.Space("a"), target);

        services.Count(static descriptor => descriptor.ServiceType == typeof(IMemoryStore)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IDocumentStore)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IVectorIndex)).ShouldBe(1);
    }
}
