// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Verifies vector retrieval over published documents, stale-version filtering, space compatibility, and isolation.</summary>
public sealed class DocumentChunkRetrievalSourceTests
{
    private static async Task PublishAsync(MemoryHarness harness, MemoryTestOwner owner, DocumentId id, string version, string text, string key) =>
        _ = await harness.Provider.GetRequiredService<IDocumentLifecycleCoordinator>()
            .PublishAsync(DocumentHarness.Publish(owner, id, version, text, key), TestContext.Current.CancellationToken);

    [Fact]
    public async Task RetrieveAsync_WhenADocumentMatchesTheQueryVector_ReturnsItsActiveChunkWithProvenance()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var xDocument = new DocumentId(Guid.NewGuid());
        await PublishAsync(harness, owner, xDocument, "v1", "xxx about excellent things", "p-1");
        await PublishAsync(harness, owner, new DocumentId(Guid.NewGuid()), "v1", "yyy about other things", "p-2");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "x"), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        var top = result.Candidates.First(static candidate => candidate.DocumentId is not null);
        top.DocumentId.ShouldBe(xDocument);
        _ = top.ChunkId.ShouldNotBeNull();
        top.Trust.ShouldBe(TrustClassification.UntrustedData);
        top.Content.Text.ShouldContain("xxx");
        top.Provenance.SourceKind.ShouldBe("test");
    }

    [Fact]
    public async Task RetrieveAsync_WhenADocumentWasSuperseded_NeverReturnsTheOldVersionsText()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var id = new DocumentId(Guid.NewGuid());
        await PublishAsync(harness, owner, id, "v1", "xxx the old policy", "p-1");
        await PublishAsync(harness, owner, id, "v2", "xxx the new policy", "p-2");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "x"), TestContext.Current.CancellationToken);

        var texts = result.Candidates.Where(static candidate => candidate.DocumentId is not null).Select(static candidate => candidate.Content.Text).ToArray();
        texts.ShouldAllBe(static text => text.Contains("new policy", StringComparison.Ordinal));
        texts.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RetrieveAsync_WhenAnotherTenantQueries_ReturnsNoDocumentCandidates()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner("tenant-a");
        var stranger = MemoryTestData.NewOwner("tenant-b");
        await PublishAsync(harness, owner, new DocumentId(Guid.NewGuid()), "v1", "xxx confidential material", "p-1");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(stranger, "x"), TestContext.Current.CancellationToken);

        result.Candidates.Any(static candidate => candidate.DocumentId is not null).ShouldBeFalse();
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheDocumentIsNotShared_ASecondPrincipalInTheTenantCannotSeeIt()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner("tenant-a", "owner");
        var colleague = MemoryTestData.NewOwner("tenant-a", "colleague");
        await PublishAsync(harness, owner, new DocumentId(Guid.NewGuid()), "v1", "xxx private notes", "p-1");

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(colleague, "x"), TestContext.Current.CancellationToken);

        result.Candidates.Any(static candidate => candidate.DocumentId is not null).ShouldBeFalse();
    }

    [Fact]
    public async Task RetrieveAsync_WhenNoIndexSharesTheQueryEmbeddingSpace_FailsBeforeContactingAnyIndex()
    {
        using var harness = DocumentHarness.Create(
            arrange: services => services.AddInMemoryVectorIndex(MemoryTestData.Space("other", 3, model: "another-model")),
            profile: configured =>
            {
                configured.VectorIndexes = [new VectorIndexKey("other")];
                configured.RetrievalSources = [MemoryRetrievalSourceKeys.Documents];
            },
            withVectors: false);
        var owner = MemoryTestData.NewOwner();

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "x"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.SourcesUnavailable);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheEmbeddingModelFails_DegradesToTheSourcesThatNeedNoEmbedding()
    {
        using var harness = DocumentHarness.Create();
        var owner = MemoryTestData.NewOwner();
        var remembered = await harness.Coordinator.ProposeAsync(MemoryTestData.Proposal(owner, "xxx remembered in memory"), TestContext.Current.CancellationToken);
        harness.Provider.GetRequiredService<DocumentHarness.LetterEmbeddings>().Fail = true;

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(owner, "xxx remembered"), TestContext.Current.CancellationToken);

        result.IsCompleted.ShouldBeTrue();
        result.Candidates.Select(static candidate => candidate.MemoryId).ShouldContain(remembered.Record!.Id);
        result.Summary!.SourcesUnavailable.ShouldBe(1);
    }

    [Fact]
    public async Task RetrieveAsync_WhenTheDocumentSourceIsSelectedWithoutADocumentStore_ReportsTheSourceUnavailable()
    {
        using var harness = MemoryHarness.Create(
            arrange: services => services.AddDocumentRetrievalSource(),
            profile: configured => configured.RetrievalSources = [MemoryRetrievalSourceKeys.Documents]);

        var result = await harness.Pipeline.RetrieveAsync(MemoryTestData.Query(MemoryTestData.NewOwner(), "x"), TestContext.Current.CancellationToken);

        result.Failure!.Kind.ShouldBe(RetrievalFailureKind.SourcesUnavailable);
    }
}
