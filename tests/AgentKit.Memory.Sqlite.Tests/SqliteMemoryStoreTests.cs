// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite.Tests;

/// <summary>Verifies <see cref="SqliteMemoryStore"/> construction, schema and identity binding, and persistence across reopening.</summary>
public sealed class SqliteMemoryStoreTests: IDisposable
{
    private readonly SqliteMemoryTestDatabase _database = new();
    private readonly TestGoalGrants _grants = new();

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var key = new MemoryStoreKey("k");
        var target = _database.Target();
        var settings = SqliteMemorySettings.CreateDefault();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new SqliteMemoryStore(default, target, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new SqliteMemoryStore(key, null!, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteMemoryStore(key, target, null!, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteMemoryStore(key, target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new SqliteMemoryStore(key, target, settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new SqliteMemoryStore(key, target, settings, _grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Descriptor_WhenRead_ClaimsDurabilityAndNamesItsAudience()
    {
        var store = Open();

        store.Descriptor.IsDurable.ShouldBeTrue();
        store.Descriptor.SecurityAudience.ShouldBe(new ComponentId("agentkit.memory.sqlite"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var store = Open();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.WriteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ListAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.TransitionAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenBuilt_DoesNotOpenOrCreateTheDatabase()
    {
        _ = Open();

        File.Exists(_database.PathOf()).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndCreationIsAllowed_CreatesItAndIsIdempotent()
    {
        var store = Open();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        File.Exists(_database.PathOf()).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndOnlyExistingOnesAreAllowed_Fails()
    {
        var store = Open(_database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSchemaIsMissingAndOnlyExactSchemasAreAllowed_Fails()
    {
        await using (var connection = new SqliteConnection($"Data Source={_database.PathOf()};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
        }

        var store = Open(_database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnotherInstance_FailsClosed()
    {
        await Open().InitializeAsync(TestContext.Current.CancellationToken);
        var other = Open(_database.Target(id: new SqliteMemoryInstanceId(Guid.NewGuid())));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await other.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnotherStoreFamily_FailsClosed()
    {
        var documents = new SqliteDocumentStore(new DocumentStoreKey("d"), _database.Target(), SqliteMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
        await documents.InitializeAsync(TestContext.Current.CancellationToken);
        var memory = Open();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await memory.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_WhenTheRecordExceedsTheConfiguredSizeBound_FailsWithoutPersistingAnything()
    {
        var owner = MemoryTestData.NewOwner();
        var store = new SqliteMemoryStore(new MemoryStoreKey("memory"), _database.Target(), new SqliteMemorySettings(TimeSpan.FromSeconds(1), 512), _grants, new TestIntentIds(), TimeProvider.System);
        var request = new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Write(MemoryTestData.Record(owner, new string('x', 4_000)), owner.Authorization);

        _ = await Should.ThrowAsync<InvalidDataException>(async () => await store.WriteAsync(request, TestContext.Current.CancellationToken));

        (await store.ReadAsync(new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Read(request.Record.Id, owner.Authorization), TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    [Fact]
    public async Task ReadAsync_WhenAnotherStoreInstanceSharesTheFile_SeesAcknowledgedWrites()
    {
        var owner = MemoryTestData.NewOwner();
        var writer = Open();
        var reader = Open();
        var record = MemoryTestData.Record(owner);
        _ = await writer.WriteAsync(new MemoryStoreRequestFactory(_grants, writer.Descriptor.SecurityAudience).Write(record, owner.Authorization), TestContext.Current.CancellationToken);

        var read = await reader.ReadAsync(new MemoryStoreRequestFactory(_grants, reader.Descriptor.SecurityAudience).Read(record.Id, owner.Authorization), TestContext.Current.CancellationToken);

        read.Record.ShouldBe(record);
    }

    private SqliteMemoryStore Open(SqliteMemoryTarget? target = null) =>
        new(new MemoryStoreKey("memory"), target ?? _database.Target(), SqliteMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
}
