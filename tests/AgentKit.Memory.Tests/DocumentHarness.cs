// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Tests;

/// <summary>Adds an in-memory document store, a vector index, a deterministic embedding model, and the document source to the memory harness.</summary>
internal static class DocumentHarness
{
    internal static DocumentStoreKey DocumentsKey { get; } = new("docs");

    internal static ComponentKey<IEmbeddingModelSelector> SelectorKey { get; } = new("tests.embed-selector");

    internal static ComponentKey<IEmbeddingRequestExecutor> ExecutorKey { get; } = new("tests.embed-executor");

    internal static VectorSpaceDescriptor Space { get; } = MemoryTestData.Space("vectors");

    internal static MemoryHarness Create(Action<IServiceCollection>? arrange = null, Action<MemoryProfileOptions>? profile = null, bool withVectors = true)
    {
        var harness = MemoryHarness.Create(
            arrange: services =>
            {
                _ = services.AddInMemoryDocumentStore(DocumentsKey);
                _ = services.AddSingleton<IModelCatalog>(new StaticModelCatalog(new ModelCatalogSnapshot(new ModelCatalogVersion(1), [])));
                _ = services.AddSingleton<LetterEmbeddings>();
                _ = services.AddMemoryEmbeddingSelector<LetterEmbeddingSelector>(SelectorKey);
                _ = services.AddMemoryEmbeddingExecutor<LetterEmbeddingExecutor>(ExecutorKey);
                _ = services.AddDocumentRetrievalSource();
                if (withVectors)
                {
                    _ = services.AddInMemoryVectorIndex(Space);
                }

                arrange?.Invoke(services);
            },
            profile: configured =>
            {
                configured.DocumentStore = DocumentsKey;
                configured.EmbeddingSelectorKey = SelectorKey;
                configured.EmbeddingExecutorKey = ExecutorKey;
                configured.EmbeddingModels = [new EmbeddingModelAlias("embed")];
                configured.RetrievalSources = [MemoryRetrievalSourceKeys.Documents, MemoryRetrievalSourceKeys.DurableMemory];
                if (withVectors)
                {
                    configured.VectorIndexes = [Space.IndexKey];
                }

                profile?.Invoke(configured);
            });
        return harness;
    }

    internal static DocumentPublishCommand Publish(MemoryTestOwner owner, DocumentId id, string version, string text, string key, bool shared = false, DataClassification classification = DataClassification.Internal) => new(
        owner.Context, id, new DocumentVersion(version), text, new DocumentMetadata("Title", "text/plain", null, ExtensionData.Empty), classification,
        new Provenance("test", owner.RunId, owner.SessionId), new RetentionPolicy(), shared, new IdempotencyKey(key));

    /// <summary>Holds the observable behavior of the fake embedding model.</summary>
    internal sealed class LetterEmbeddings
    {
        internal int Calls { get; set; }

        internal bool Fail { get; set; }

        internal static EmbeddingSpaceIdentity SpaceFor(EmbeddingPurpose purpose) => MemoryTestData.EmbeddingSpace(3, "embed-1", purpose);

        /// <summary>Maps text to its counts of the letters x, y, and z plus a tiny constant so no vector is zero.</summary>
        internal static ImmutableArray<float> Vector(string text) =>
        [
            text.Count(static character => character is 'x' or 'X') + 0.001f,
            text.Count(static character => character is 'y' or 'Y') + 0.001f,
            text.Count(static character => character is 'z' or 'Z') + 0.001f,
        ];
    }

    internal sealed class LetterEmbeddingSelector: IEmbeddingModelSelector
    {
        public ValueTask<EmbeddingSelectionResult> SelectAsync(EmbeddingSelectionRequest request, CancellationToken cancellationToken = default)
        {
            var descriptor = new EmbeddingModelDescriptor(
                new EmbeddingModelAlias("embed"), new ProviderId("test"), new ApiFamilyId("embeddings"), new ModelId("embed-1"), null,
                new EmbeddingCapabilities(true, true, true, true, true, ExtensionData.Empty), new EmbeddingLimits(null, null, null, null), null, ExtensionData.Empty);
            return ValueTask.FromResult<EmbeddingSelectionResult>(new EmbeddingModelSelected(new EmbeddingSelectionDecision(descriptor, "test", new ModelCatalogVersion(1))));
        }
    }

    internal sealed class LetterEmbeddingExecutor(LetterEmbeddings model): IEmbeddingRequestExecutor
    {
        public Task<EmbeddingExecutionResult> ExecuteAsync(EmbeddingExecutionRequest request, CancellationToken cancellationToken = default)
        {
            model.Calls++;
            if (model.Fail)
            {
                throw new InvalidOperationException("The embedding model failed.");
            }

            var items = request.Request.Inputs
                .Select((input, index) => (EmbeddingItemOutcome) new EmbeddingItemSucceeded(
                    index, null, new DenseFloatVector(LetterEmbeddings.Vector(((TextEmbeddingInput) input).Text)), LetterEmbeddings.SpaceFor(request.Request.Purpose), ExtensionData.Empty))
                .ToImmutableArray();
            return Task.FromResult<EmbeddingExecutionResult>(
                new EmbeddingExecutionCompleted(request.Selection, 1, new EmbeddingResponse(items, ModelUsage.NotReported, null, ExtensionData.Empty)));
        }
    }
}
