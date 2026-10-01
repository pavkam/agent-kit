// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite.Tests;

using AgentKit.Observability;

/// <summary>Verifies the SQLite adapter's durability across reopening, exclusive ownership, schema binding, and integrity handling.</summary>
public sealed class SqliteArtifactStoreTests
{
    private static readonly byte[] _content = "durable bytes"u8.ToArray();

    [Fact]
    public async Task Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var target = fixture.Database.Target();
        var settings = SqliteArtifactSettings.CreateDefault();
        var ids = new SequentialIntentIds();

        Should.Throw<ArgumentNullException>(() => new SqliteArtifactStore(null!, settings, fixture.GrantStore, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new SqliteArtifactStore(target, null!, fixture.GrantStore, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new SqliteArtifactStore(target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new SqliteArtifactStore(target, settings, fixture.GrantStore, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new SqliteArtifactStore(target, settings, fixture.GrantStore, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public async Task SecurityAudience_WhenRead_NamesTheSqliteBackend()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();

        (await fixture.CreateAsync(TestContext.Current.CancellationToken)).SecurityAudience.ShouldBe(new ComponentId("agentkit.artifacts.sqlite"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.PrepareAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.FinalizeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.AbortAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task Constructor_WhenBuilt_DoesNotOpenOrCreateTheDatabase()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();

        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        File.Exists(fixture.Database.PathOf()).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndCreationIsAllowed_CreatesItAndIsIdempotent()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = (SqliteArtifactStore) await fixture.CreateAsync(TestContext.Current.CancellationToken);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        File.Exists(fixture.Database.PathOf()).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseIsMissingAndOnlyExistingOnesAreAllowed_Fails()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        using var store = Open(fixture, fixture.Database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheSchemaIsMissingAndOnlyExactSchemasAreAllowed_Fails()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        await using (var connection = new SqliteConnection($"Data Source={fixture.Database.PathOf()};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
        }

        using var store = Open(fixture, fixture.Database.Target(open: SqliteDatabaseOpenMode.OpenExisting, schema: SqliteSchemaMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDirectoryIsMissing_Fails()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var missing = new SqliteArtifactTarget(
            Path.Combine(Path.GetDirectoryName(fixture.Database.PathOf())!, "nowhere", "store.db"), new SqliteArtifactInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing, SqliteSchemaMode.ApplyKnownMigrations);
        using var store = Open(fixture, missing);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheDatabaseBelongsToAnotherInstance_FailsClosed()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        using (var first = Open(fixture, fixture.Database.Target()))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var other = Open(fixture, fixture.Database.Target(id: new SqliteArtifactInstanceId(Guid.NewGuid())));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await other.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("foreign-tables")]
    [InlineData("no-identity")]
    [InlineData("unsupported-version")]
    public async Task InitializeAsync_WhenTheSchemaIsNotTheSupportedLayout_FailsClosed(string defect)
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        using (var first = Open(fixture, fixture.Database.Target()))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        await using (var connection = new SqliteConnection($"Data Source={fixture.Database.PathOf()};Pooling=False"))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = defect switch
            {
                "foreign-tables" => "CREATE TABLE intruder(x INTEGER)",
                "no-identity" => "DELETE FROM artifact_metadata",
                _ => "UPDATE artifact_metadata SET schema_version = 99",
            };
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        using var reopened = Open(fixture, fixture.Database.Target());

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await reopened.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenAnotherStoreHoldsTheFile_RejectsTheSecondOwner()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        using var first = Open(fixture, fixture.Database.Target());
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = new SqliteArtifactStore(
            fixture.Database.Target(), new SqliteArtifactSettings(TimeSpan.FromSeconds(1), 1_048_576), fixture.GrantStore, new SequentialIntentIds(), TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reopen_WhenContentWasFinalized_RetainsBytesAndAuthoritativeMetadata()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);

        var reopened = fixture.Reopen();

        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
        reference.ProfileKey.ShouldBe(new ArtifactProfileKey("conformance"));
    }

    [Fact]
    public async Task Reopen_WhenContentWasOnlyPrepared_KeepsStagingFinalizableAndNeverReadable()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var receipt = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();

        var reopened = fixture.Reopen();
        var finalize = fixture.CreateFinalize(receipt.PreparationId, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        var finalized = await reopened.FinalizeAsync(finalize, TestContext.Current.CancellationToken);

        var reference = finalized.ShouldBeOfType<ArtifactStoreFinalized>().Reference;
        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task Reopen_WhenAnEquivalentPrepareReplays_ReturnsTheOriginalReceipt()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = fixture.CreatePrepare(_content, idempotencyKey: "replay");
        await fixture.RegisterGrantAsync(first.Grant, TestContext.Current.CancellationToken);
        var original = await store.PrepareAsync(first, TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();
        var retry = fixture.CreatePrepare(_content, idempotencyKey: "replay", lifetime: first.ExpiresAt - first.CreatedAt);
        await fixture.RegisterGrantAsync(retry.Grant, TestContext.Current.CancellationToken);

        (await reopened.PrepareAsync(retry, TestContext.Current.CancellationToken)).ShouldBe(original);
    }

    [Fact]
    public async Task Reopen_WhenAVersionWasDeleted_TombstonePersistsAndPreventsRebind()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        var delete = fixture.CreateDelete(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);
        _ = await store.DeleteAsync(delete, TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        var replacement = fixture.CreatePrepare("replacement"u8.ToArray(), idempotencyKey: "replacement", artifactId: reference.Id, version: reference.Version);
        await fixture.RegisterGrantAsync(replacement.Grant, TestContext.Current.CancellationToken);
        _ = await reopened.PrepareAsync(replacement, TestContext.Current.CancellationToken);
        var finalize = fixture.CreateFinalize(replacement.PreparationId, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);

        (await reopened.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        (await reopened.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreFinalizeRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Conflict);
    }

    [Fact]
    public async Task Reopen_WhenTwoTenantsShareBytes_KeepsThemPartitionedAndDeletesOnlyTheOwnersCopy()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var a = await CommitAsync(fixture, store, fixture.PrimaryIdentity);
        var b = await CommitAsync(fixture, store, fixture.SecondaryIdentity);
        var reopened = fixture.Reopen();
        var delete = fixture.CreateDelete(a, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);

        _ = await reopened.DeleteAsync(delete, TestContext.Current.CancellationToken);

        (await ReadTextAsync(fixture, reopened, b, fixture.SecondaryIdentity)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task ReadAsync_WhenStoredBytesWereTamperedWith_ReportsUnavailableNeverTheAlteredContent()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        fixture.Reopen().Dispose();
        await TamperAsync(fixture, "UPDATE artifact_payloads SET content = x'00'");
        var reopened = fixture.Reopen();
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);

        var result = await reopened.ReadAsync(read, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task InitializeAsync_WhenAnUnreferencedPayloadWasLeftByACrash_SweepsItOnRecovery()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        fixture.Reopen().Dispose();
        await TamperAsync(fixture, $"INSERT INTO artifact_payloads(tenant, content_hash, content) VALUES ('tenant-a', 'sha256:orphan', x'01')");

        var reopened = fixture.Reopen();
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
        fixture.Reopen().Dispose();

        (await CountAsync(fixture, "SELECT COUNT(*) FROM artifact_payloads WHERE content_hash = 'sha256:orphan'")).ShouldBe(0L);
        (await CountAsync(fixture, "SELECT COUNT(*) FROM artifact_payloads")).ShouldBe(1L);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheEntryExceedsTheConfiguredSizeBound_ReportsUnavailableAndPersistsNothing()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        fixture.Reopen().Dispose();
        using var store = new SqliteArtifactStore(
            fixture.Database.Target(), new SqliteArtifactSettings(TimeSpan.FromSeconds(1), 64), fixture.GrantStore, new SequentialIntentIds(), fixture.ClockProvider);
        var request = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);

        var result = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        store.Dispose();
        (await CountAsync(fixture, "SELECT COUNT(*) FROM artifact_payloads")).ShouldBe(0L);
        (await CountAsync(fixture, "SELECT COUNT(*) FROM artifact_entries")).ShouldBe(0L);
    }

    [Fact]
    public async Task PrepareAsync_WhenObserved_LabelsTheAdapterAsSqlite()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var unique = TestExecutionIdentity.Create(new TenantId($"tenant-{Guid.NewGuid():N}"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var request = fixture.CreatePrepare(_content, unique);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == request.TenantId.Value);

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.ArtifactStoreAdapter).ShouldBe("sqlite");
    }

    [Fact]
    public async Task Dispose_WhenCalledRepeatedly_IsIdempotentAndReleasesTheFile()
    {
        await using var fixture = new SqliteArtifactStoreConformanceFixture();
        using var first = Open(fixture, fixture.Database.Target());
        await first.InitializeAsync(TestContext.Current.CancellationToken);

        first.Dispose();
        first.Dispose();
        using var second = Open(fixture, fixture.Database.Target());

        await second.InitializeAsync(TestContext.Current.CancellationToken);
    }

    private static SqliteArtifactStore Open(SqliteArtifactStoreConformanceFixture fixture, SqliteArtifactTarget target) =>
        new(target, SqliteArtifactSettings.CreateDefault(), fixture.GrantStore, new SequentialIntentIds(), TimeProvider.System);

    private static async Task<ArtifactReference> CommitAsync(SqliteArtifactStoreConformanceFixture fixture, IArtifactStore store, ExecutionIdentity? identity = null)
    {
        var who = identity ?? fixture.PrimaryIdentity;
        var prepare = fixture.CreatePrepare(_content, who, idempotencyKey: $"commit-{Guid.NewGuid():N}");
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var prepared = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();
        var finalize = fixture.CreateFinalize(prepared.PreparationId, who);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        return (await store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreFinalized>().Reference;
    }

    private static async Task<string> ReadTextAsync(SqliteArtifactStoreConformanceFixture fixture, SqliteArtifactStore store, ArtifactReference reference, ExecutionIdentity? identity = null)
    {
        var read = fixture.CreateRead(reference, identity ?? fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        await using var opened = (await store.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadOpened>();
        using var reader = new StreamReader(opened.Content);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static async Task TamperAsync(SqliteArtifactStoreConformanceFixture fixture, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={fixture.Database.PathOf()};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> CountAsync(SqliteArtifactStoreConformanceFixture fixture, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={fixture.Database.PathOf()};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long) (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
