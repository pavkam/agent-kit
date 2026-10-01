// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

using AgentKit.Observability;

/// <summary>Verifies the JSON adapter's bootstrap, locking, recovery, compaction, payload layout, and integrity handling.</summary>
public sealed class JsonArtifactStoreTests
{
    private static readonly byte[] _content = "durable bytes"u8.ToArray();

    [Fact]
    public async Task Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var target = fixture.Root.Target();
        var settings = JsonArtifactSettings.CreateDefault();
        var ids = new SequentialIntentIds();

        Should.Throw<ArgumentNullException>(() => new JsonArtifactStore(null!, settings, fixture.GrantStore, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactStore(target, null!, fixture.GrantStore, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactStore(target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactStore(target, settings, fixture.GrantStore, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactStore(target, settings, fixture.GrantStore, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public async Task SecurityAudience_WhenRead_NamesTheJsonBackend()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();

        (await fixture.CreateAsync(TestContext.Current.CancellationToken)).SecurityAudience.ShouldBe(new ComponentId("agentkit.artifacts.json"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.PrepareAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.FinalizeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.AbortAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public async Task Constructor_WhenBuilt_DoesNotOpenLockOrWriteAnything()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();

        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);

        File.Exists(fixture.Root.ManifestPath()).ShouldBeFalse();
        Directory.Exists(fixture.Root.PayloadDirectory()).ShouldBeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRootIsNewAndCreationIsAllowed_CreatesTheManifestAndPayloadDirectoryAndIsIdempotent()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = (JsonArtifactStore) await fixture.CreateAsync(TestContext.Current.CancellationToken);

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        File.Exists(fixture.Root.ManifestPath()).ShouldBeTrue();
        Directory.Exists(fixture.Root.PayloadDirectory()).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRootHasNoManifestAndOnlyExistingOnesAreAllowed_Fails()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        using var store = Open(fixture, fixture.Root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRootBelongsToAnotherInstance_FailsClosed()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        using (var first = Open(fixture, fixture.Root.Target()))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var other = Open(fixture, fixture.Root.Target(id: new JsonArtifactInstanceId(Guid.NewGuid())));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await other.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheRootWasWrittenUnderADifferentEncodingContract_FailsClosed()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        using (var first = Open(fixture, fixture.Root.Target()))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        var contract = JsonStoreSerialization.CreateCanonicalOptions();
        contract.PropertyNameCaseInsensitive = true;
        var settings = new JsonArtifactSettings(1_048_576, 1_048_576, 1_048_576, 4_096, new JsonEncodingSettings(contract));
        using var other = new JsonArtifactStore(fixture.Root.Target(), settings, fixture.GrantStore, new SequentialIntentIds(), TimeProvider.System);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await other.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenASecondWriterOpensTheSameRoot_IsRejectedWhileTheFirstHoldsTheLock()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        using var first = Open(fixture, fixture.Root.Target());
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = Open(fixture, fixture.Root.Target());

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_AfterTheFirstStoreIsDisposed_LetsAnotherWriterOpenTheRoot()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var first = Open(fixture, fixture.Root.Target());
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        first.Dispose();
        first.Dispose();
        using var second = Open(fixture, fixture.Root.Target());

        await second.InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Operations_WhenTheStoreIsDisposed_ThrowObjectDisposedException()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = Open(fixture, fixture.Root.Target());
        var request = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        store.Dispose();

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.PrepareAsync(request, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reopen_WhenContentWasFinalized_RetainsBytesAndAuthoritativeMetadata()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);

        var reopened = fixture.Reopen();

        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task Reopen_WhenContentWasOnlyPrepared_KeepsStagingFinalizableAndNeverReadable()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var prepare = fixture.CreatePrepare(_content);
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var receipt = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();

        var reopened = fixture.Reopen();
        var finalize = fixture.CreateFinalize(receipt.PreparationId, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        var finalized = await reopened.FinalizeAsync(finalize, TestContext.Current.CancellationToken);

        (await ReadTextAsync(fixture, reopened, finalized.ShouldBeOfType<ArtifactStoreFinalized>().Reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task Reopen_WhenAVersionWasDeleted_TombstonePersistsAndItsPayloadFileIsGone()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        var delete = fixture.CreateDelete(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);
        _ = await store.DeleteAsync(delete, TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);

        (await reopened.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
        PayloadFiles(fixture).ShouldBeEmpty();
    }

    [Fact]
    public async Task Payloads_WhenTwoTenantsStoreIdenticalBytes_AreSeparateFilesAndOneTenantsDeleteLeavesTheOther()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var a = await CommitAsync(fixture, store, fixture.PrimaryIdentity);
        var b = await CommitAsync(fixture, store, fixture.SecondaryIdentity);
        PayloadFiles(fixture).Count.ShouldBe(2);
        var delete = fixture.CreateDelete(a, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);

        _ = await store.DeleteAsync(delete, TestContext.Current.CancellationToken);

        PayloadFiles(fixture).Count.ShouldBe(1);
        (await ReadTextAsync(fixture, store, b, fixture.SecondaryIdentity)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task Payloads_WhenOneTenantStoresIdenticalBytesTwice_ShareOneFileUntilTheLastReferenceIsDeleted()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var first = await CommitAsync(fixture, store, fixture.PrimaryIdentity);
        var second = await CommitAsync(fixture, store, fixture.PrimaryIdentity);
        PayloadFiles(fixture).Count.ShouldBe(1);

        _ = await DeleteAsync(fixture, store, first);
        PayloadFiles(fixture).Count.ShouldBe(1);
        (await ReadTextAsync(fixture, store, second)).ShouldBe("durable bytes");
        _ = await DeleteAsync(fixture, store, second);

        PayloadFiles(fixture).ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadAsync_WhenTheLogIsAppendedToOnlyAfterAPayload_NeverLeavesAnEntryWithoutBytes()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        fixture.Reopen().Dispose();
        foreach (var file in PayloadFiles(fixture))
        {
            File.Delete(file);
        }

        var reopened = fixture.Reopen();
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);

        (await reopened.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task ReadAsync_WhenAPayloadFileWasTamperedWith_ReportsUnavailableNeverTheAlteredContent()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        await File.WriteAllBytesAsync(PayloadFiles(fixture).Single(), "tampered"u8.ToArray(), TestContext.Current.CancellationToken);
        var read = fixture.CreateRead(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);

        (await store.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
    }

    [Fact]
    public async Task InitializeAsync_WhenUnreferencedPayloadsAndTemporaryFilesWereLeftByACrash_SweepsThemAndKeepsLiveOnes()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        var directory = Path.GetDirectoryName(PayloadFiles(fixture).Single())!;
        fixture.Reopen().Dispose();
        await File.WriteAllBytesAsync(Path.Combine(directory, new string('a', 64) + ".bin"), [1], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "stale.bin.tmp"), [1], TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root.PayloadDirectory(), "unknown-tenant"));
        await File.WriteAllBytesAsync(Path.Combine(fixture.Root.PayloadDirectory(), "unknown-tenant", "x.bin"), [1], TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        PayloadFiles(fixture).Count.ShouldBe(1);
        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsAllowed_RecoversEveryAcknowledgedEntry()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var reference = await CommitAsync(fixture, store);
        fixture.Reopen().Dispose();
        await File.AppendAllTextAsync(fixture.Root.LogPath(), "{\"entries\":[{\"tenant\":\"torn", TestContext.Current.CancellationToken);

        var reopened = fixture.Reopen();

        (await ReadTextAsync(fixture, reopened, reference)).ShouldBe("durable bytes");
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsNotAllowed_Fails()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        _ = await CommitAsync(fixture, store);
        fixture.Reopen().Dispose();
        await File.AppendAllTextAsync(fixture.Root.LogPath(), "{\"entries\":[", TestContext.Current.CancellationToken);
        using var strict = Open(fixture, fixture.Root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await strict.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogExceedsTheCompactionThreshold_RewritesOneSnapshotPerEntryAndKeepsTombstones()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var settings = new JsonArtifactSettings(1_048_576, 1_048_576, 1_048_576, 3, JsonEncodingSettings.CreateDefault());
        ArtifactReference kept;
        ArtifactReference doomed;
        using (var store = new JsonArtifactStore(fixture.Root.Target(), settings, fixture.GrantStore, new SequentialIntentIds(), fixture.ClockProvider))
        {
            kept = await CommitAsync(fixture, store);
            doomed = await CommitAsync(fixture, store, artifact: new ArtifactId(Guid.Parse("70000000-0000-0000-0000-000000000007")));
            _ = await DeleteAsync(fixture, store, doomed);
        }

        using var reopened = new JsonArtifactStore(fixture.Root.Target(), settings, fixture.GrantStore, new SequentialIntentIds(), fixture.ClockProvider);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        File.ReadAllLines(fixture.Root.LogPath()).Length.ShouldBe(2);
        (await ReadTextAsync(fixture, reopened, kept)).ShouldBe("durable bytes");
        var read = fixture.CreateRead(doomed, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        (await reopened.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.NotFound);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheEntryCannotFitTheConfiguredRecordBound_ReportsUnavailableAndLeavesNoPayload()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var settings = new JsonArtifactSettings(1_500, 1_048_576, 1_048_576, 4_096, JsonEncodingSettings.CreateDefault());
        using var store = new JsonArtifactStore(fixture.Root.Target(), settings, fixture.GrantStore, new SequentialIntentIds(), fixture.ClockProvider);
        var declared = fixture.CreateMetadata(_content);
        var oversized = new ArtifactMetadata(
            declared.OwnerId, new string('m', 4_000), declared.DeclaredLength, declared.DeclaredContentHash, declared.Classification,
            declared.Ownership, declared.Mutability, declared.Retention, null);
        var request = fixture.CreatePrepare(_content, metadata: oversized);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);

        var result = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Unavailable);
        PayloadFiles(fixture).ShouldBeEmpty();
        File.Exists(fixture.Root.LogPath()).ShouldBeFalse();
    }

    [Fact]
    public async Task PrepareAsync_WhenObserved_LabelsTheAdapterAsJson()
    {
        await using var fixture = new JsonArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var unique = TestExecutionIdentity.Create(new TenantId($"tenant-{Guid.NewGuid():N}"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var request = fixture.CreatePrepare(_content, unique);
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == request.TenantId.Value);

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.ArtifactStoreAdapter).ShouldBe("json");
    }

    private static JsonArtifactStore Open(JsonArtifactStoreConformanceFixture fixture, JsonArtifactTarget target) =>
        new(target, JsonArtifactSettings.CreateDefault(), fixture.GrantStore, new SequentialIntentIds(), TimeProvider.System);

    private static List<string> PayloadFiles(JsonArtifactStoreConformanceFixture fixture) =>
        Directory.Exists(fixture.Root.PayloadDirectory())
            ? [.. Directory.EnumerateFiles(fixture.Root.PayloadDirectory(), "*.bin", SearchOption.AllDirectories)]
            : [];

    private static async Task<ArtifactReference> CommitAsync(
        JsonArtifactStoreConformanceFixture fixture, IArtifactStore store, ExecutionIdentity? identity = null, ArtifactId? artifact = null)
    {
        var who = identity ?? fixture.PrimaryIdentity;
        var prepare = fixture.CreatePrepare(_content, who, idempotencyKey: $"commit-{Guid.NewGuid():N}", artifactId: artifact);
        await fixture.RegisterGrantAsync(prepare.Grant, TestContext.Current.CancellationToken);
        var prepared = (await store.PrepareAsync(prepare, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStorePrepared>();
        var finalize = fixture.CreateFinalize(prepared.PreparationId, who);
        await fixture.RegisterGrantAsync(finalize.Grant, TestContext.Current.CancellationToken);
        return (await store.FinalizeAsync(finalize, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreFinalized>().Reference;
    }

    private static async Task<ArtifactStoreDeleteResult> DeleteAsync(JsonArtifactStoreConformanceFixture fixture, IArtifactStore store, ArtifactReference reference)
    {
        var delete = fixture.CreateDelete(reference, fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(delete.Grant, TestContext.Current.CancellationToken);
        return await store.DeleteAsync(delete, TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadTextAsync(
        JsonArtifactStoreConformanceFixture fixture, IArtifactStore store, ArtifactReference reference, ExecutionIdentity? identity = null)
    {
        var read = fixture.CreateRead(reference, identity ?? fixture.PrimaryIdentity);
        await fixture.RegisterGrantAsync(read.Grant, TestContext.Current.CancellationToken);
        await using var opened = (await store.ReadAsync(read, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactStoreReadOpened>();
        using var reader = new StreamReader(opened.Content);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
