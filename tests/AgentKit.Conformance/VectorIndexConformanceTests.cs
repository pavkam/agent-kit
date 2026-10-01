// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

using AgentKit.TestSupport;

/// <summary>Defines portable space-compatibility, search ranking, deletion propagation, isolation, and authorization behavior for <see cref="IVectorIndex"/>.</summary>
/// <typeparam name="TFixture">The adapter-specific isolated fixture.</typeparam>
/// <remarks>Every case is required contract behavior for all vector-index adapters. Durability cases run when the fixture declares <see cref="ConformanceCapabilities.SupportsDurability"/>.</remarks>
public abstract class VectorIndexConformanceTests<TFixture>
    where TFixture : IVectorIndexConformanceFixture, new()
{
    /// <summary>Verifies the index is an exact scan and never claims approximate search.</summary>
    [Fact]
    public void ApproximateSearch_WhenRead_IsFalseForAnExactScanAndDurabilityMatchesTheFixture()
    {
        var fixture = new TFixture();

        fixture.Index.ApproximateSearch.ShouldBeFalse();
        fixture.Index.IsDurable.ShouldBe(fixture.Capabilities.SupportsDurability);
        fixture.Index.SecurityAudience.Value.ShouldNotBeNullOrWhiteSpace();
        fixture.Index.VectorSpace.IsCompatibleWith(MemoryTestData.Space()).ShouldBeTrue();
    }

    /// <summary>Verifies the nearest vector under cosine distance ranks first and ties break deterministically.</summary>
    [Fact]
    public async Task SearchAsync_WhenVectorsAreStored_RanksByCosineSimilarity()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 3);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]), (chunks[1], [0.9f, 0.1f, 0f]), (chunks[2], [0f, 1f, 0f]));

        var result = await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 3);

        result.IsSearched.ShouldBeTrue();
        result.Matches.Select(static match => match.ChunkId).ShouldBe([chunks[0].Id, chunks[1].Id, chunks[2].Id]);
        result.Matches[0].Score.ShouldBe(1d, 1e-6);
        result.Matches[2].Score.ShouldBe(0d, 1e-6);
    }

    /// <summary>Verifies the topK bound limits the matches.</summary>
    [Fact]
    public async Task SearchAsync_WhenTopKIsSmallerThanTheIndex_ReturnsOnlyTheBestMatches()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 3);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]), (chunks[1], [0.5f, 0.5f, 0f]), (chunks[2], [0f, 1f, 0f]));

        (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 2)).Matches.Length.ShouldBe(2);
    }

    /// <summary>Verifies identical scores are ordered by chunk identity so ranking is deterministic.</summary>
    [Fact]
    public async Task SearchAsync_WhenScoresTie_OrdersByChunkIdentity()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 3);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]), (chunks[1], [1f, 0f, 0f]), (chunks[2], [1f, 0f, 0f]));

        var result = await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 3);

        result.Matches.Select(static match => match.ChunkId.Value).ShouldBe([.. chunks.Select(static chunk => chunk.Id.Value).Order()]);
    }

    /// <summary>Verifies Euclidean distance ranks the closest vector first with a higher-is-better score.</summary>
    [Fact]
    public async Task SearchAsync_WhenTheMetricIsEuclidean_RanksTheClosestVectorFirst()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var space = MemoryTestData.Space("euclid", metric: VectorDistanceMetric.Euclidean);
        var index = fixture.CreateIndex(space);
        var chunks = Chunks(owner, 2);
        _ = await UpsertAsync(fixture, index, owner, space, (chunks[0], [10f, 0f, 0f]), (chunks[1], [1f, 1f, 0f]));

        var result = await SearchAsync(fixture, index, owner, space, [0f, 0f, 0f], 2);

        result.Matches[0].ChunkId.ShouldBe(chunks[1].Id);
        result.Matches[0].Score.ShouldBeGreaterThan(result.Matches[1].Score);
    }

    /// <summary>Verifies the dot product ranks the vector with the largest inner product first.</summary>
    [Fact]
    public async Task SearchAsync_WhenTheMetricIsDotProduct_RanksTheLargestInnerProductFirst()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var space = MemoryTestData.Space("dot", metric: VectorDistanceMetric.DotProduct);
        var index = fixture.CreateIndex(space);
        var chunks = Chunks(owner, 2);
        _ = await UpsertAsync(fixture, index, owner, space, (chunks[0], [1f, 0f, 0f]), (chunks[1], [3f, 0f, 0f]));

        var result = await SearchAsync(fixture, index, owner, space, [1f, 0f, 0f], 2);

        result.Matches[0].ChunkId.ShouldBe(chunks[1].Id);
        result.Matches[0].Score.ShouldBe(3d, 1e-6);
    }

    /// <summary>Verifies upserting an existing chunk replaces its vector.</summary>
    [Fact]
    public async Task UpsertAsync_WhenTheChunkAlreadyHasAVector_ReplacesIt()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 1);
        _ = await UpsertAsync(fixture, fixture.Index, owner, "u1", (chunks[0], [1f, 0f, 0f]));
        _ = await UpsertAsync(fixture, fixture.Index, owner, "u2", (chunks[0], [0f, 1f, 0f]));

        var result = await SearchAsync(fixture, fixture.Index, owner, [0f, 1f, 0f], 5);

        result.Matches.ShouldHaveSingleItem().Score.ShouldBe(1d, 1e-6);
    }

    /// <summary>Verifies the watermark advances with mutations and is reported by searches.</summary>
    [Fact]
    public async Task UpsertAsync_WhenMutating_AdvancesTheWatermarkThatSearchesReport()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 2);
        var first = await UpsertAsync(fixture, fixture.Index, owner, "u1", (chunks[0], [1f, 0f, 0f]));
        var second = await UpsertAsync(fixture, fixture.Index, owner, "u2", (chunks[1], [0f, 1f, 0f]));

        second.Watermark.ShouldBeGreaterThan(first.Watermark);
        (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 5)).Watermark.ShouldBe(second.Watermark);
    }

    /// <summary>Verifies an equivalent upsert replay is harmless and a different batch under the same key is refused.</summary>
    [Fact]
    public async Task UpsertAsync_WhenReplayedWithTheSameKey_ReplaysAndRefusesADifferentBatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 2);
        var first = await UpsertAsync(fixture, fixture.Index, owner, "u", (chunks[0], [1f, 0f, 0f]));

        var replay = await UpsertAsync(fixture, fixture.Index, owner, "u", (chunks[0], [1f, 0f, 0f]));
        var conflict = await UpsertAsync(fixture, fixture.Index, owner, "u", (chunks[1], [0f, 1f, 0f]));

        replay.Replayed.ShouldBeTrue();
        replay.Watermark.ShouldBe(first.Watermark);
        conflict.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IdempotencyConflict);
    }

    /// <summary>Verifies a matching dimension with a different model revision is rejected before any state is touched.</summary>
    [Fact]
    public async Task UpsertAsync_WhenSpaceHasTheSameDimensionsButAnotherModel_RejectsBeforeTouchingState()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var foreign = MemoryTestData.Space(model: "embed-2");
        var chunks = Chunks(owner, 1);
        var before = (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 5)).Watermark;

        var result = await UpsertAsync(fixture, fixture.Index, owner, foreign, "u", (chunks[0], [1f, 0f, 0f]));

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IncompatibleVectorSpace);
        var after = await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 5);
        after.Matches.ShouldBeEmpty();
        after.Watermark.ShouldBe(before);
    }

    /// <summary>Verifies searches against a different index key, metric, or dimension are rejected before searching and consume no grant.</summary>
    [Fact]
    public async Task SearchAsync_WhenSpaceDiffersInKeyMetricOrDimensions_RejectsBeforeSearching()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 1);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]));
        var consumed = fixture.Grants.ConsumedCount;

        foreach (var space in new[]
        {
            MemoryTestData.Space("another-index"),
            MemoryTestData.Space(metric: VectorDistanceMetric.Euclidean),
            MemoryTestData.Space(dimensions: 4),
        })
        {
            var query = ImmutableArray.CreateRange(Enumerable.Repeat(1f, space.Dimensions));
            var result = await fixture.Index.SearchAsync(
                new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Search(space, query, 3, owner.Authorization),
                TestContext.Current.CancellationToken);
            result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IncompatibleVectorSpace);
        }

        fixture.Grants.ConsumedCount.ShouldBe(consumed);
    }

    /// <summary>Verifies a query embedded by the same model revision with a different purpose or response identifier is compatible.</summary>
    [Fact]
    public async Task SearchAsync_WhenOnlyPurposeAndResponseIdentifiersDiffer_IsCompatible()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 1);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]));
        var queryEmbedding = MemoryTestData.EmbeddingSpace(purpose: EmbeddingPurpose.Query);
        var queryCompatibleSpace = new VectorSpaceDescriptor(fixture.Index.VectorSpace.IndexKey, queryEmbedding, fixture.Index.VectorSpace.DistanceMetric);

        var result = await SearchAsync(fixture, fixture.Index, owner, queryCompatibleSpace, [1f, 0f, 0f], 3);

        _ = result.Matches.ShouldHaveSingleItem();
    }

    /// <summary>Verifies deletion removes vectors from search results and deleting an absent chunk is not an error.</summary>
    [Fact]
    public async Task DeleteAsync_WhenChunksAreRemoved_PropagatesToSearchAndToleratesAbsentChunks()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 3);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]), (chunks[1], [0f, 1f, 0f]), (chunks[2], [0f, 0f, 1f]));

        var deleted = await DeleteAsync(fixture, fixture.Index, owner, "d1", chunks[0].Id, new ChunkId(Guid.NewGuid()));
        var replay = await DeleteAsync(fixture, fixture.Index, owner, "d2", chunks[0].Id);

        deleted.Deleted.ShouldBe(1);
        replay.IsDeleted.ShouldBeTrue();
        replay.Deleted.ShouldBe(0);
        (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 10)).Matches.Select(static match => match.ChunkId).ShouldBe([chunks[1].Id, chunks[2].Id], ignoreOrder: true);
    }

    /// <summary>Verifies a deletion with an incompatible space is refused and removes nothing.</summary>
    [Fact]
    public async Task DeleteAsync_WhenSpaceIsIncompatible_RejectsAndRemovesNothing()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 1);
        _ = await UpsertAsync(fixture, fixture.Index, owner, (chunks[0], [1f, 0f, 0f]));
        var foreign = MemoryTestData.Space(model: "embed-2");
        var request = new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Delete(foreign, [chunks[0].Id], owner.Authorization);

        (await fixture.Index.DeleteAsync(request, TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IncompatibleVectorSpace);
        _ = (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 5)).Matches.ShouldHaveSingleItem();
    }

    /// <summary>Verifies searches never return another tenant's, another agent's, or another principal's private vectors.</summary>
    [Fact]
    public async Task SearchAsync_WhenOtherScopesHoldVectors_NeverReturnsThem()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var colleague = MemoryTestData.Owner(owner.AgentId, owner.SessionId, owner.RunId, "tenant", "colleague");
        var outsider = MemoryTestData.NewOwner("other-tenant");
        var stranger = MemoryTestData.NewOwner();
        var own = Chunks(owner, 1)[0];
        var colleaguesPrivate = Chunks(colleague, 1)[0];
        var colleaguesShared = Chunks(colleague, 1)[0];
        var outsiders = Chunks(outsider, 1)[0];
        var strangers = Chunks(stranger, 1)[0];
        _ = await UpsertAsync(fixture, fixture.Index, owner, "u1", (own, [1f, 0f, 0f]));
        _ = await UpsertAsync(fixture, fixture.Index, colleague, "u2", (colleaguesPrivate, [1f, 0f, 0f]));
        _ = await UpsertAsync(fixture, fixture.Index, colleague, "u3", shared: true, (colleaguesShared, [1f, 0f, 0f]));
        _ = await UpsertAsync(fixture, fixture.Index, outsider, "u4", (outsiders, [1f, 0f, 0f]));
        _ = await UpsertAsync(fixture, fixture.Index, stranger, "u5", (strangers, [1f, 0f, 0f]));

        var result = await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 10);

        result.Matches.Select(static match => match.ChunkId).ShouldBe([own.Id, colleaguesShared.Id], ignoreOrder: true);
    }

    /// <summary>Verifies a document restriction narrows results to the named documents.</summary>
    [Fact]
    public async Task SearchAsync_WhenDocumentsAreNamed_RestrictsResultsToThem()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var first = Chunks(owner, 1)[0];
        var second = Chunks(owner, 1)[0];
        _ = await UpsertAsync(fixture, fixture.Index, owner, (first, [1f, 0f, 0f]), (second, [1f, 0f, 0f]));

        var result = await fixture.Index.SearchAsync(
            new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Search(fixture.Index.VectorSpace, [1f, 0f, 0f], 10, owner.Authorization, [second.DocumentId]),
            TestContext.Current.CancellationToken);

        result.Matches.ShouldHaveSingleItem().ChunkId.ShouldBe(second.Id);
    }

    /// <summary>Verifies a vector claiming another agent than the authorized scope is refused.</summary>
    [Fact]
    public async Task UpsertAsync_WhenVectorClaimsAnotherAgent_RejectsWithScopeMismatch()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var stranger = MemoryTestData.NewOwner();
        var chunk = Chunks(stranger, 1)[0];
        var record = MemoryTestData.Vector(stranger, chunk, 1, 0, 0);
        var request = new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Upsert(fixture.Index.VectorSpace, [record], owner.Authorization);

        (await fixture.Index.UpsertAsync(request, TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.ScopeMismatch);
    }

    /// <summary>Verifies a grant bound to a different batch is denied before anything is stored.</summary>
    [Fact]
    public async Task UpsertAsync_WhenGrantBindsADifferentBatch_DeniesBeforeAnyWrite()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 2);
        var factory = new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience);
        var forged = factory.Upsert(fixture.Index.VectorSpace, [MemoryTestData.Vector(owner, chunks[1], 0, 1, 0)], owner.Authorization, "u");
        var request = new VectorUpsertRequest(fixture.Index.VectorSpace, [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)], new IdempotencyKey("u"), forged.Grant);

        (await fixture.Index.UpsertAsync(request, TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 5)).Matches.ShouldBeEmpty();
    }

    /// <summary>Verifies an unavailable grant store fails a search closed.</summary>
    [Fact]
    public async Task SearchAsync_WhenGrantStoreIsUnavailable_Denies()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var request = new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Search(fixture.Index.VectorSpace, [1f, 0f, 0f], 3, owner.Authorization);
        fixture.Grants.Fail = true;

        var result = await fixture.Index.SearchAsync(request, TestContext.Current.CancellationToken);
        fixture.Grants.Fail = false;

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    /// <summary>Verifies a consumed single-use grant cannot authorize a second search.</summary>
    [Fact]
    public async Task SearchAsync_WhenGrantWasAlreadyConsumed_Denies()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var request = new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Search(fixture.Index.VectorSpace, [1f, 0f, 0f], 3, owner.Authorization);
        _ = await fixture.Index.SearchAsync(request, TestContext.Current.CancellationToken);

        (await fixture.Index.SearchAsync(request, TestContext.Current.CancellationToken)).Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
    }

    /// <summary>Verifies a cancelled token stops an upsert before it commits.</summary>
    [Fact]
    public async Task UpsertAsync_WhenCancelled_ThrowsWithoutWriting()
    {
        var fixture = new TFixture();
        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 1);
        var request = new VectorIndexRequestFactory(fixture.Grants, fixture.Index.SecurityAudience).Upsert(fixture.Index.VectorSpace, [MemoryTestData.Vector(owner, chunks[0], 1, 0, 0)], owner.Authorization);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await fixture.Index.UpsertAsync(request, cancelled.Token));

        (await SearchAsync(fixture, fixture.Index, owner, [1f, 0f, 0f], 5)).Matches.ShouldBeEmpty();
    }

    /// <summary>Verifies acknowledged vectors, deletions, and the watermark survive reopening a durable index.</summary>
    [Fact]
    public async Task ReopenAsync_WhenTheIndexIsDurable_RetainsVectorsDeletionsAndWatermark()
    {
        var fixture = new TFixture();
        if (!fixture.Capabilities.SupportsDurability)
        {
            return;
        }

        var owner = MemoryTestData.NewOwner();
        var chunks = Chunks(owner, 2);
        _ = await UpsertAsync(fixture, fixture.Index, owner, "u", (chunks[0], [1f, 0f, 0f]), (chunks[1], [0f, 1f, 0f]));
        var deleted = await DeleteAsync(fixture, fixture.Index, owner, "d", chunks[1].Id);

        var reopened = await fixture.ReopenAsync(TestContext.Current.CancellationToken);

        var result = await reopened.SearchAsync(
            new VectorIndexRequestFactory(fixture.Grants, reopened.SecurityAudience).Search(reopened.VectorSpace, [1f, 0f, 0f], 10, owner.Authorization),
            TestContext.Current.CancellationToken);
        result.Matches.ShouldHaveSingleItem().ChunkId.ShouldBe(chunks[0].Id);
        result.Watermark.ShouldBe(deleted.Watermark);
    }

    private static ImmutableArray<DocumentChunk> Chunks(MemoryTestOwner owner, int count) => MemoryTestData.Chunks(MemoryTestData.Document(owner), count);

    private static Task<VectorUpsertResult> UpsertAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, params (DocumentChunk Chunk, float[] Vector)[] items) =>
        UpsertAsync(fixture, index, owner, "upsert-1", items);

    private static Task<VectorUpsertResult> UpsertAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, string key, params (DocumentChunk Chunk, float[] Vector)[] items) =>
        UpsertAsync(fixture, index, owner, index.VectorSpace, key, items);

    private static Task<VectorUpsertResult> UpsertAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, VectorSpaceDescriptor space, params (DocumentChunk Chunk, float[] Vector)[] items) =>
        UpsertAsync(fixture, index, owner, space, "upsert-1", items);

    private static Task<VectorUpsertResult> UpsertAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, string key, bool shared, params (DocumentChunk Chunk, float[] Vector)[] items) =>
        UpsertRecordsAsync(fixture, index, owner, index.VectorSpace, key, [.. items.Select(item => Record(owner, item.Chunk, item.Vector, shared))]);

    private static Task<VectorUpsertResult> UpsertAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, VectorSpaceDescriptor space, string key, params (DocumentChunk Chunk, float[] Vector)[] items) =>
        UpsertRecordsAsync(fixture, index, owner, space, key, [.. items.Select(item => Record(owner, item.Chunk, item.Vector, false))]);

    private static VectorRecord Record(MemoryTestOwner owner, DocumentChunk chunk, float[] vector, bool shared) => new(
        chunk.Id, chunk.DocumentId, chunk.Version, owner.AgentId, new PrincipalVisibility(owner.Identity.TenantId, owner.Identity.PrincipalId, shared),
        [.. vector], chunk.Hash, chunk.Chunker, MemoryTestData.Now);

    private static async Task<VectorUpsertResult> UpsertRecordsAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, VectorSpaceDescriptor space, string key, ImmutableArray<VectorRecord> records) =>
        await index.UpsertAsync(new VectorIndexRequestFactory(fixture.Grants, index.SecurityAudience).Upsert(space, records, owner.Authorization, key), TestContext.Current.CancellationToken);

    private static Task<VectorSearchResult> SearchAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, ImmutableArray<float> query, int topK) =>
        SearchAsync(fixture, index, owner, index.VectorSpace, query, topK);

    private static async Task<VectorSearchResult> SearchAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, VectorSpaceDescriptor space, ImmutableArray<float> query, int topK) =>
        await index.SearchAsync(new VectorIndexRequestFactory(fixture.Grants, index.SecurityAudience).Search(space, query, topK, owner.Authorization), TestContext.Current.CancellationToken);

    private static async Task<VectorDeleteResult> DeleteAsync(TFixture fixture, IVectorIndex index, MemoryTestOwner owner, string key, params ChunkId[] chunkIds) =>
        await index.DeleteAsync(new VectorIndexRequestFactory(fixture.Grants, index.SecurityAudience).Delete(index.VectorSpace, [.. chunkIds], owner.Authorization, key), TestContext.Current.CancellationToken);
}
