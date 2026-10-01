// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Verifies <see cref="SqliteDocumentStore"/> construction and atomic publication.</summary>
public sealed class SqliteDocumentStoreTests: IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();
    private readonly TestGoalGrants _grants = new();

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var key = new DocumentStoreKey("k");
        var target = _database.Target();
        var settings = SqliteMemorySettings.CreateDefault();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new SqliteDocumentStore(default, target, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new SqliteDocumentStore(key, null!, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteDocumentStore(key, target, null!, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteDocumentStore(key, target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new SqliteDocumentStore(key, target, settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new SqliteDocumentStore(key, target, settings, _grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var store = Open();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.WriteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ActivateAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task WriteAsync_WhenTheRecordExceedsTheSizeBound_LeavesThePriorVersionActive()
    {
        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner, "v1");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        var store = Open(new SqliteMemorySettings(TimeSpan.FromSeconds(1), 6_000));
        var factory = new DocumentStoreRequestFactory(_grants, store.Descriptor.SecurityAudience);
        _ = await store.WriteAsync(factory.Write(v1, MemoryTestData.Chunks(v1, 1), true, owner.Authorization, "k1"), TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidDataException>(async () => await store.WriteAsync(factory.Write(v2, MemoryTestData.Chunks(v2, 40), true, owner.Authorization, "k2"), TestContext.Current.CancellationToken));

        var read = await store.ReadAsync(factory.Read(v1.Id, null, true, owner.Authorization), TestContext.Current.CancellationToken);
        read.Record!.Version.ShouldBe(v1.Version);
        read.Chunks.Length.ShouldBe(1);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnotherInstance_FailsClosed()
    {
        await Open().InitializeAsync(TestContext.Current.CancellationToken);
        var other = new SqliteDocumentStore(new DocumentStoreKey("d"), _database.Target(id: new SqliteMemoryInstanceId(Guid.NewGuid())), SqliteMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await other.InitializeAsync(TestContext.Current.CancellationToken));
    }

    private SqliteDocumentStore Open(SqliteMemorySettings? settings = null) =>
        new(new DocumentStoreKey("documents"), _database.Target(), settings ?? SqliteMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
}
