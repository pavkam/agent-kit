// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Verifies <see cref="JsonMemoryStore"/> bootstrap, locking, torn-append recovery, compaction, and failure behavior.</summary>
public sealed class JsonMemoryStoreTests: IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();
    private readonly TestGoalGrants _grants = new();

    /// <inheritdoc/>
    public void Dispose() => _root.Dispose();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var key = new MemoryStoreKey("k");
        var target = _root.Target();
        var settings = JsonMemorySettings.CreateDefault();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new JsonMemoryStore(default, target, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new JsonMemoryStore(key, null!, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonMemoryStore(key, target, null!, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonMemoryStore(key, target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new JsonMemoryStore(key, target, settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new JsonMemoryStore(key, target, settings, _grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Descriptor_WhenRead_ClaimsDurabilityAndNamesItsAudience()
    {
        using var store = Open();

        store.Descriptor.IsDurable.ShouldBeTrue();
        store.Descriptor.SecurityAudience.ShouldBe(new ComponentId("agentkit.memory.json"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        using var store = Open();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.WriteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ListAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.TransitionAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Fact]
    public void Constructor_WhenBuilt_DoesNotOpenLockOrWriteAnything()
    {
        using var store = Open();

        Directory.EnumerateFileSystemEntries(_root.StoreDirectory("store")).ShouldBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestIsMissingAndCreationIsAllowed_CreatesItAndIsIdempotent()
    {
        using var store = Open();

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        File.Exists(_root.ManifestPath()).ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestIsMissingAndOnlyExistingRootsAreAllowed_Fails()
    {
        using var store = Open(_root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestBelongsToAnotherInstance_FailsClosed()
    {
        using (var first = Open())
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var other = Open(_root.Target(id: new JsonMemoryInstanceId(Guid.NewGuid())));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await other.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheManifestBelongsToAnotherStoreKind_FailsClosed()
    {
        using (var documents = new JsonDocumentStore(new DocumentStoreKey("d"), _root.Target(), JsonMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System))
        {
            await documents.InitializeAsync(TestContext.Current.CancellationToken);
        }

        using var memory = Open();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await memory.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenASecondWriterOpensTheSameRoot_IsRejectedWhileTheFirstHoldsTheLock()
    {
        using var first = Open();
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        using var second = Open();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await second.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_AfterTheFirstStoreIsDisposed_LetsAnotherWriterOpenTheRoot()
    {
        var first = Open();
        await first.InitializeAsync(TestContext.Current.CancellationToken);
        first.Dispose();
        first.Dispose();
        using var second = Open();

        await second.InitializeAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Operations_WhenTheStoreIsDisposed_ThrowObjectDisposedException()
    {
        var store = Open();
        store.Dispose();
        var owner = MemoryTestData.NewOwner();
        var request = new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Read(new MemoryId(Guid.NewGuid()), owner.Authorization);

        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.ReadAsync(request, TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await store.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsAllowed_RecoversEveryAcknowledgedRecord()
    {
        var owner = MemoryTestData.NewOwner();
        var record = MemoryTestData.Record(owner, "acknowledged");
        using (var store = Open())
        {
            _ = await store.WriteAsync(new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Write(record, owner.Authorization), TestContext.Current.CancellationToken);
        }

        await File.AppendAllTextAsync(_root.LogPath("store", "memory"), "{\"entries\":[{\"tenant\":\"torn", TestContext.Current.CancellationToken);
        using var reopened = Open();

        var read = await reopened.ReadAsync(new MemoryStoreRequestFactory(_grants, reopened.Descriptor.SecurityAudience).Read(record.Id, owner.Authorization), TestContext.Current.CancellationToken);

        read.Record.ShouldBe(record);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppendAndRecoveryIsNotAllowed_Fails()
    {
        var owner = MemoryTestData.NewOwner();
        using (var store = Open())
        {
            _ = await store.WriteAsync(new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Write(MemoryTestData.Record(owner), owner.Authorization), TestContext.Current.CancellationToken);
        }

        await File.AppendAllTextAsync(_root.LogPath("store", "memory"), "{\"entries\":[", TestContext.Current.CancellationToken);
        using var strict = Open(_root.Target(mode: JsonStoreOpenMode.OpenExisting, recovery: JsonStoreRecoveryMode.ValidateExact));

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await strict.InitializeAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogExceedsTheCompactionThreshold_RewritesOneSnapshotPerRecordAndKeepsTombstones()
    {
        var owner = MemoryTestData.NewOwner();
        var settings = new JsonMemorySettings(16_777_216, 1_048_576, 3, JsonEncodingSettings.CreateDefault());
        var record = MemoryTestData.Record(owner, state: MemoryLifecycleState.Proposed);
        var doomed = MemoryTestData.Record(owner, "doomed");
        using (var store = Open(settings: settings))
        {
            var factory = new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience);
            _ = await store.WriteAsync(factory.Write(record, owner.Authorization, "k1"), TestContext.Current.CancellationToken);
            _ = await store.WriteAsync(factory.Write(doomed, owner.Authorization, "k2"), TestContext.Current.CancellationToken);
            _ = await store.TransitionAsync(factory.Transition(record.Id, MemoryLifecycleState.Validated, "1", owner.Authorization), TestContext.Current.CancellationToken);
            _ = await store.TransitionAsync(factory.Transition(record.Id, MemoryLifecycleState.Accepted, "2", owner.Authorization, key: "t2"), TestContext.Current.CancellationToken);
            _ = await store.DeleteAsync(factory.Delete(doomed.Id, MemoryDeleteMode.Purge, owner.Authorization), TestContext.Current.CancellationToken);
        }

        using var reopened = Open(settings: settings);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);
        var factory2 = new MemoryStoreRequestFactory(_grants, reopened.Descriptor.SecurityAudience);

        File.ReadAllLines(_root.LogPath("store", "memory")).Length.ShouldBe(2);
        (await reopened.ReadAsync(factory2.Read(record.Id, owner.Authorization), TestContext.Current.CancellationToken)).Record!.State.ShouldBe(MemoryLifecycleState.Accepted);
        (await reopened.ReadAsync(factory2.Read(doomed.Id, owner.Authorization), TestContext.Current.CancellationToken)).Tombstone!.Purged.ShouldBeTrue();
        File.ReadAllText(_root.LogPath("store", "memory")).ShouldNotContain("doomed");
    }

    [Fact]
    public async Task WriteAsync_WhenTheEncodingCannotFitTheConfiguredRecordBound_RejectsAsUnavailableAndPersistsNothing()
    {
        var owner = MemoryTestData.NewOwner();
        var settings = new JsonMemorySettings(3_000, 1_048_576, 4_096, JsonEncodingSettings.CreateDefault());
        using var store = Open(settings: settings);
        var request = new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Write(MemoryTestData.Record(owner, new string('x', 8_000)), owner.Authorization);

        var result = await store.WriteAsync(request, TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Unavailable);
        (await store.ReadAsync(new MemoryStoreRequestFactory(_grants, store.Descriptor.SecurityAudience).Read(request.Record.Id, owner.Authorization), TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    private JsonMemoryStore Open(JsonMemoryTarget? target = null, JsonMemorySettings? settings = null) =>
        new(new MemoryStoreKey("memory"), target ?? _root.Target(), settings ?? JsonMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
}
