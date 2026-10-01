// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Verifies <see cref="JsonDocumentStore"/> construction, locking, and torn-append recovery of the active pointer.</summary>
public sealed class JsonDocumentStoreTests: IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();
    private readonly TestGoalGrants _grants = new();

    /// <inheritdoc/>
    public void Dispose() => _root.Dispose();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var key = new DocumentStoreKey("k");
        var target = _root.Target();
        var settings = JsonMemorySettings.CreateDefault();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new JsonDocumentStore(default, target, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("key");
        Should.Throw<ArgumentNullException>(() => new JsonDocumentStore(key, null!, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonDocumentStore(key, target, null!, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonDocumentStore(key, target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new JsonDocumentStore(key, target, settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new JsonDocumentStore(key, target, settings, _grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        using var store = Open();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.WriteAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ActivateAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
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
    public async Task InitializeAsync_WhenAPublicationWasTornAfterAnAcknowledgedOne_KeepsThePriorVersionActive()
    {
        var owner = MemoryTestData.NewOwner();
        var v1 = MemoryTestData.Document(owner, "v1");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        using (var store = Open())
        {
            var factory = new DocumentStoreRequestFactory(_grants, store.Descriptor.SecurityAudience);
            _ = await store.WriteAsync(factory.Write(v1, MemoryTestData.Chunks(v1), true, owner.Authorization, "k1"), TestContext.Current.CancellationToken);
        }

        await File.AppendAllTextAsync(_root.LogPath("store", "documents"), "{\"tenant\":\"torn-publication-of-v2", TestContext.Current.CancellationToken);
        using var reopened = Open();
        var read = await reopened.ReadAsync(new DocumentStoreRequestFactory(_grants, reopened.Descriptor.SecurityAudience).Read(v1.Id, null, true, owner.Authorization), TestContext.Current.CancellationToken);

        read.Record!.Version.ShouldBe(v1.Version);
        read.ActiveVersion.ShouldBe(v1.Version);
        v2.Version.ShouldNotBe(read.Record.Version);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogExceedsTheCompactionThreshold_RewritesOneSnapshotPerDocument()
    {
        var owner = MemoryTestData.NewOwner();
        var settings = new JsonMemorySettings(16_777_216, 1_048_576, 2, JsonEncodingSettings.CreateDefault());
        var v1 = MemoryTestData.Document(owner, "v1");
        var v2 = MemoryTestData.Document(owner, "v2", "sha256:bb", v1.Id);
        using (var store = Open(settings))
        {
            var factory = new DocumentStoreRequestFactory(_grants, store.Descriptor.SecurityAudience);
            _ = await store.WriteAsync(factory.Write(v1, MemoryTestData.Chunks(v1), true, owner.Authorization, "k1"), TestContext.Current.CancellationToken);
            _ = await store.WriteAsync(factory.Write(v2, MemoryTestData.Chunks(v2), true, owner.Authorization, "k2"), TestContext.Current.CancellationToken);
            var v3 = MemoryTestData.Document(owner, "v3", "sha256:cc", v1.Id);
            _ = await store.WriteAsync(factory.Write(v3, MemoryTestData.Chunks(v3), true, owner.Authorization, "k3"), TestContext.Current.CancellationToken);
        }

        using var reopened = Open(settings);
        await reopened.InitializeAsync(TestContext.Current.CancellationToken);

        File.ReadAllLines(_root.LogPath("store", "documents")).Length.ShouldBe(1);
    }

    private JsonDocumentStore Open(JsonMemorySettings? settings = null) =>
        new(new DocumentStoreKey("documents"), _root.Target(), settings ?? JsonMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
}
