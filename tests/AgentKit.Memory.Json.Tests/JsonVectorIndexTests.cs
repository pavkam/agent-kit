// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Verifies <see cref="JsonVectorIndex"/> construction, locking, recovery, and compaction.</summary>
public sealed class JsonVectorIndexTests: IDisposable
{
    private readonly JsonMemoryTestRoot _root = new();
    private readonly TestGoalGrants _grants = new();

    /// <inheritdoc/>
    public void Dispose() => _root.Dispose();

    [Fact]
    public void Constructor_WhenADependencyIsInvalid_ThrowsNamingIt()
    {
        var space = MemoryTestData.Space();
        var target = _root.Target();
        var settings = JsonMemorySettings.CreateDefault();
        var ids = new TestIntentIds();

        Should.Throw<ArgumentNullException>(() => new JsonVectorIndex(null!, target, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("space");
        Should.Throw<ArgumentNullException>(() => new JsonVectorIndex(space, null!, settings, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => new JsonVectorIndex(space, target, null!, _grants, ids, TimeProvider.System)).ParamName.ShouldBe("settings");
        Should.Throw<ArgumentNullException>(() => new JsonVectorIndex(space, target, settings, null!, ids, TimeProvider.System)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new JsonVectorIndex(space, target, settings, _grants, null!, TimeProvider.System)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new JsonVectorIndex(space, target, settings, _grants, ids, null!)).ParamName.ShouldBe("time");
    }

    [Fact]
    public void Constructor_WhenConstructed_ClaimsADurableExactScan()
    {
        using var index = Open();

        index.IsDurable.ShouldBeTrue();
        index.ApproximateSearch.ShouldBeFalse();
        index.SecurityAudience.ShouldBe(new ComponentId("agentkit.vectors.json"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        using var index = Open();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.UpsertAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.SearchAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await index.DeleteAsync(null!))).ParamName.ShouldBe("request");
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
    public async Task InitializeAsync_WhenTheLogEndsWithATornAppend_RecoversEveryAcknowledgedBatch()
    {
        var owner = MemoryTestData.NewOwner();
        var chunk = MemoryTestData.Chunks(MemoryTestData.Document(owner), 1)[0];
        using (var index = Open())
        {
            var factory = new VectorIndexRequestFactory(_grants, index.SecurityAudience);
            _ = await index.UpsertAsync(factory.Upsert(index.VectorSpace, [MemoryTestData.Vector(owner, chunk, 1, 0, 0)], owner.Authorization), TestContext.Current.CancellationToken);
        }

        await File.AppendAllTextAsync(_root.LogPath("store", "vectors"), "{\"tenant\":\"torn", TestContext.Current.CancellationToken);
        using var reopened = Open();

        var result = await reopened.SearchAsync(new VectorIndexRequestFactory(_grants, reopened.SecurityAudience).Search(reopened.VectorSpace, [1f, 0f, 0f], 3, owner.Authorization), TestContext.Current.CancellationToken);

        result.Matches.ShouldHaveSingleItem().ChunkId.ShouldBe(chunk.Id);
    }

    [Fact]
    public async Task InitializeAsync_WhenTheLogExceedsTheCompactionThreshold_KeepsVectorsReceiptsAndTheWatermark()
    {
        var owner = MemoryTestData.NewOwner();
        var settings = new JsonMemorySettings(16_777_216, 1_048_576, 2, JsonEncodingSettings.CreateDefault());
        var chunks = MemoryTestData.Chunks(MemoryTestData.Document(owner), 3);
        long watermark;
        using (var index = Open(settings))
        {
            var factory = new VectorIndexRequestFactory(_grants, index.SecurityAudience);
            _ = await index.UpsertAsync(factory.Upsert(index.VectorSpace, [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)], owner.Authorization, "u1"), TestContext.Current.CancellationToken);
            _ = await index.UpsertAsync(factory.Upsert(index.VectorSpace, [MemoryTestData.Vector(owner, chunks[1], 0, 1, 0)], owner.Authorization, "u2"), TestContext.Current.CancellationToken);
            watermark = (await index.DeleteAsync(factory.Delete(index.VectorSpace, [chunks[1].Id], owner.Authorization, "d1"), TestContext.Current.CancellationToken)).Watermark;
        }

        using var reopened = Open(settings);
        var factory2 = new VectorIndexRequestFactory(_grants, reopened.SecurityAudience);
        var search = await reopened.SearchAsync(factory2.Search(reopened.VectorSpace, [1f, 0f, 0f], 5, owner.Authorization), TestContext.Current.CancellationToken);
        var replay = await reopened.UpsertAsync(factory2.Upsert(reopened.VectorSpace, [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)], owner.Authorization, "u1"), TestContext.Current.CancellationToken);

        search.Matches.ShouldHaveSingleItem().ChunkId.ShouldBe(chunks[0].Id);
        search.Watermark.ShouldBe(watermark);
        replay.Replayed.ShouldBeTrue();
    }

    private JsonVectorIndex Open(JsonMemorySettings? settings = null) =>
        new(MemoryTestData.Space(), _root.Target(), settings ?? JsonMemorySettings.CreateDefault(), _grants, new TestIntentIds(), TimeProvider.System);
}
