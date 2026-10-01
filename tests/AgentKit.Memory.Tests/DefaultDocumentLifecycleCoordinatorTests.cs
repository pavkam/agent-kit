// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies document publication, version switching, deletion propagation, and their fail-closed guards.</summary>
public sealed class DefaultDocumentLifecycleCoordinatorTests
{
    private sealed class UncleanableIndex: IVectorIndex
    {
        public VectorSpaceDescriptor VectorSpace => DocumentHarness.Space;

        public ComponentId SecurityAudience { get; } = new("tests.uncleanable-index");

        public bool IsDurable => false;

        public bool ApproximateSearch => false;

        public ValueTask<VectorUpsertResult> UpsertAsync(VectorUpsertRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(VectorUpsertResult.Succeeded(request.Records.Length, 1, replayed: false));

        public ValueTask<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(VectorDeleteResult.Rejected(new MemoryStoreFailure(MemoryStoreFailureKind.Unavailable, "The index is unavailable.")));
    }

    private static IDocumentLifecycleCoordinator Coordinator(MemoryHarness harness) => harness.Provider.GetRequiredService<IDocumentLifecycleCoordinator>();

    private static IDocumentStore Documents(MemoryHarness harness) => harness.Provider.GetRequiredKeyedService<IDocumentStore>(DocumentHarness.DocumentsKey.Value);

    private static IVectorIndex Index(MemoryHarness harness) => harness.Provider.GetRequiredKeyedService<IVectorIndex>(DocumentHarness.Space.IndexKey.Value);

    private static async Task<int> CountVectorsAsync(MemoryHarness harness, MemoryTestOwner owner, string query)
    {
        var index = Index(harness);
        var result = await index.SearchAsync(
            new VectorIndexRequestFactory(harness.Grants, index.SecurityAudience).Search(
                DocumentHarness.Space, DocumentHarness.LetterEmbeddings.Vector(query), 50, owner.Authorization),
            TestContext.Current.CancellationToken);
        return result.Matches.Length;
    }

    private static async Task<DocumentReadResult> ReadAsync(MemoryHarness harness, MemoryTestOwner owner, DocumentId id, string? version = null)
    {
        var store = Documents(harness);
        return await store.ReadAsync(
            new DocumentStoreRequestFactory(harness.Grants, store.Descriptor.SecurityAudience).Read(id, version, true, owner.Authorization), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WhenTheProfileIsComplete_StagesEmbedsIndexesAndActivates()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.\n\nyyy beta.", "publish-1"), TestContext.Current.CancellationToken);

        result.IsPublished.ShouldBeTrue();
        result.ChunkCount.ShouldBe(1);
        result.VectorsIndexed.ShouldBe(1);
        result.PreviousActiveVersion.ShouldBeNull();
        var read = await ReadAsync(harness, owner, id);
        read.State.ShouldBe(DocumentVersionState.Active);
        (await CountVectorsAsync(harness, owner, "x")).ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_WhenANewVersionIsPublished_SwitchesActiveAndRemovesSupersededVectors()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "first version xxx.", "publish-1"), TestContext.Current.CancellationToken);

        var second = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v2", "second version yyy.", "publish-2"), TestContext.Current.CancellationToken);

        second.IsPublished.ShouldBeTrue();
        second.PreviousActiveVersion.ShouldBe(new DocumentVersion("v1"));
        (await ReadAsync(harness, owner, id)).Record!.Version.ShouldBe(new DocumentVersion("v2"));
        (await CountVectorsAsync(harness, owner, "x")).ShouldBe(1);
        var index = Index(harness);
        var matches = (await index.SearchAsync(
            new VectorIndexRequestFactory(harness.Grants, index.SecurityAudience).Search(DocumentHarness.Space, DocumentHarness.LetterEmbeddings.Vector("y"), 50, owner.Authorization),
            TestContext.Current.CancellationToken)).Matches;
        matches.ShouldAllBe(static match => match.DocumentVersion == new DocumentVersion("v2"));
    }

    [Fact]
    public async Task PublishAsync_WhenReplayedWithTheSameKey_IsIdempotent()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var command = DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx alpha.", "publish-1");
        _ = await Coordinator(harness).PublishAsync(command, TestContext.Current.CancellationToken);

        var replay = await Coordinator(harness).PublishAsync(command, TestContext.Current.CancellationToken);

        replay.IsPublished.ShouldBeTrue();
        replay.Replayed.ShouldBeTrue();
    }

    [Fact]
    public async Task PublishAsync_WhenTheEmbeddingModelFails_LeavesThePreviousVersionActive()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "first xxx.", "publish-1"), TestContext.Current.CancellationToken);
        harness.Provider.GetRequiredService<DocumentHarness.LetterEmbeddings>().Fail = true;

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v2", "second yyy.", "publish-2"), TestContext.Current.CancellationToken);

        result.IsPublished.ShouldBeFalse();
        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Unavailable);
        (await ReadAsync(harness, owner, id)).Record!.Version.ShouldBe(new DocumentVersion("v1"));
    }

    [Fact]
    public async Task PublishAsync_WhenNoIndexSharesTheEmbeddingSpace_RejectsWithIncompatibleVectorSpace()
    {
        using var harness = DocumentHarness.Create(
            arrange: services => services.AddInMemoryVectorIndex(MemoryTestData.Space("other", dimensions: 3, model: "another-model")),
            profile: configured => configured.VectorIndexes = [new VectorIndexKey("other")],
            withVectors: false);
        var owner = MemoryTestData.NewOwner();

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx.", "publish-1"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.IncompatibleVectorSpace);
    }

    [Fact]
    public async Task PublishAsync_WhenTheProfileHasNoVectorIndexes_ActivatesWithoutEmbedding()
    {
        using var harness = DocumentHarness.Create(withVectors: false);
        var owner = MemoryTestData.NewOwner();

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx.", "publish-1"), TestContext.Current.CancellationToken);

        result.IsPublished.ShouldBeTrue();
        result.VectorsIndexed.ShouldBe(0);
        harness.Provider.GetRequiredService<DocumentHarness.LetterEmbeddings>().Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PublishAsync_WhenClassificationExceedsTheProfileCeiling_RejectsBeforeAnyEffect()
    {
        using var harness = DocumentHarness.Create(profile: configured => configured.MaximumClassification = DataClassification.Internal);
        var owner = MemoryTestData.NewOwner();

        var result = await Coordinator(harness).PublishAsync(
            DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx.", "publish-1", classification: DataClassification.Restricted), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        harness.Authority.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task PublishAsync_WhenTheProfileNamesNoDocumentStore_RejectsAsUnavailable()
    {
        using var harness = MemoryHarness.Create();
        var owner = MemoryTestData.NewOwner();

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx.", "publish-1"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Unavailable);
    }

    [Fact]
    public async Task PublishAsync_WhenTheAuthorityDeniesEmbeddingEgress_RejectsWithoutCallingTheModel()
    {
        using var harness = DocumentHarness.Create();
        harness.Authority.DenyEffect = SecurityEffect.Egress;
        var owner = MemoryTestData.NewOwner();

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx.", "publish-1"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.Denied);
        harness.Provider.GetRequiredService<DocumentHarness.LetterEmbeddings>().Calls.ShouldBe(0);
    }

    [Fact]
    public async Task PublishAsync_WhenAnotherPrincipalPublishesTheSameDocument_IsRejected()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner("tenant-a", "owner");
        var stranger = MemoryTestData.NewOwner("tenant-a", "stranger");
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx.", "publish-1"), TestContext.Current.CancellationToken);

        var result = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(stranger, id, "v2", "yyy.", "publish-2"), TestContext.Current.CancellationToken);

        result.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public async Task PublishAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        using var harness = DocumentHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Coordinator(harness).PublishAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("command");
    }

    [Fact]
    public async Task PublishAsync_WhenAlreadyCancelled_PropagatesCancellation()
    {
        using var harness = DocumentHarness.Create();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var command = DocumentHarness.Publish(MemoryTestData.NewOwner(), new DocumentId(Guid.NewGuid()), "v1", "xxx.", "publish-1");

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await Coordinator(harness).PublishAsync(command, source.Token));
    }

    [Fact]
    public async Task DeleteAsync_WhenPurging_RemovesVectorsFromEveryIndexAndPurgesTheBody()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.", "publish-1"), TestContext.Current.CancellationToken);

        var result = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(owner.Context, id, DocumentDeleteMode.Purge, new IdempotencyKey("delete-1")), TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeTrue();
        result.Receipt.PhysicallyPurged.ShouldBeTrue();
        result.Receipt.PendingStores.ShouldBeEmpty();
        result.VectorsRemoved.ShouldBe(1);
        (await CountVectorsAsync(harness, owner, "x")).ShouldBe(0);
        (await ReadAsync(harness, owner, id)).IsFound.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WhenTombstoning_KeepsTheBodyButRemovesVectors()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.", "publish-1"), TestContext.Current.CancellationToken);

        var result = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(owner.Context, id, DocumentDeleteMode.Tombstone, new IdempotencyKey("delete-1")), TestContext.Current.CancellationToken);

        result.Receipt!.LogicallyDeleted.ShouldBeTrue();
        result.Receipt.PhysicallyPurged.ShouldBeFalse();
        (await CountVectorsAsync(harness, owner, "x")).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteAsync_WhenAVectorIndexCannotBeCleaned_WithholdsThePurgeAndNamesTheIndex()
    {
        using var harness = DocumentHarness.Create(
            arrange: services => services.AddVectorIndex<UncleanableIndex>(DocumentHarness.Space.IndexKey),
            withVectors: false,
            profile: configured => configured.VectorIndexes = [DocumentHarness.Space.IndexKey]);
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.", "publish-1"), TestContext.Current.CancellationToken);

        var result = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(owner.Context, id, DocumentDeleteMode.Purge, new IdempotencyKey("delete-1")), TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeTrue();
        result.Receipt.LogicallyDeleted.ShouldBeTrue();
        result.Receipt.PhysicallyPurged.ShouldBeFalse();
        result.Receipt.PendingStores.ShouldContain(DocumentHarness.Space.IndexKey.Value);
        result.Receipt.PendingStores.ShouldContain(DocumentHarness.DocumentsKey.Value);
        result.VectorsRemoved.ShouldBe(0);
    }

    [Fact]
    public async Task PublishAsync_WhenCompleted_LogsTheDocumentOperationEventWithoutContent()
    {
        var logger = new RecordingLogger<DefaultDocumentLifecycleCoordinator>();
        using var harness = DocumentHarness.Create(arrange: services => services.AddSingleton<ILogger<DefaultDocumentLifecycleCoordinator>>(logger));
        var owner = MemoryTestData.NewOwner();

        _ = await Coordinator(harness).PublishAsync(
            DocumentHarness.Publish(owner, new DocumentId(Guid.NewGuid()), "v1", "xxx protected document body.", "publish-log"), TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(32330);
        entry.Level.ShouldBe(LogLevel.Information);
        entry.Message.ShouldNotContain("protected document body");
    }

    [Fact]
    public async Task DeleteAsync_WhenAVectorIndexCannotBeCleaned_LogsTheCleanupPendingEvent()
    {
        var logger = new RecordingLogger<DefaultDocumentLifecycleCoordinator>();
        using var harness = DocumentHarness.Create(
            arrange: services =>
            {
                _ = services.AddSingleton<ILogger<DefaultDocumentLifecycleCoordinator>>(logger);
                _ = services.AddVectorIndex<UncleanableIndex>(DocumentHarness.Space.IndexKey);
            },
            withVectors: false,
            profile: configured => configured.VectorIndexes = [DocumentHarness.Space.IndexKey]);
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.", "publish-pending"), TestContext.Current.CancellationToken);

        _ = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(owner.Context, id, DocumentDeleteMode.Purge, new IdempotencyKey("delete-pending")), TestContext.Current.CancellationToken);

        var entry = logger.Snapshot().Single(static candidate => candidate.EventId.Id == 32331);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldContain(id.ToString());
    }

    [Fact]
    public async Task DeleteAsync_WhenTheDocumentIsMissing_RejectsAsNotFound()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();

        var result = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(owner.Context, new DocumentId(Guid.NewGuid()), DocumentDeleteMode.Purge, new IdempotencyKey("delete-1")), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(MemoryStoreFailureKind.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_WhenAnotherTenantDeletes_IsRejectedAndTheDocumentSurvives()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var stranger = MemoryTestData.NewOwner("tenant-b");
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.", "publish-1"), TestContext.Current.CancellationToken);

        var result = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(stranger.Context, id, DocumentDeleteMode.Purge, new IdempotencyKey("delete-1")), TestContext.Current.CancellationToken);

        result.IsDeleted.ShouldBeFalse();
        (await ReadAsync(harness, owner, id)).IsFound.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAsync_WhenCommandIsNull_ThrowsArgumentNullException()
    {
        using var harness = DocumentHarness.Create();

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await Coordinator(harness).DeleteAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("command");
    }

    [Fact]
    public async Task DeleteAsync_WhenPurgeCompletes_SubsequentRetrievalNeverReturnsTheDocument()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        _ = await Coordinator(harness).PublishAsync(DocumentHarness.Publish(owner, id, "v1", "xxx alpha.", "publish-1"), TestContext.Current.CancellationToken);
        var before = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "x"), hooks: null, TestContext.Current.CancellationToken);
        _ = await Coordinator(harness).DeleteAsync(
            new DocumentRemovalCommand(owner.Context, id, DocumentDeleteMode.Purge, new IdempotencyKey("delete-1")), TestContext.Current.CancellationToken);

        var after = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "x"), hooks: null, TestContext.Current.CancellationToken);

        before.Candidates.Any(static candidate => candidate.DocumentId is not null).ShouldBeTrue();
        after.Candidates.Any(static candidate => candidate.DocumentId is not null).ShouldBeFalse();
    }
}
