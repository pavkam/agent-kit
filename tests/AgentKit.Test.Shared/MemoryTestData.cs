// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Builds deterministic, internally consistent memory, document, and vector values for tests.</summary>
public static class MemoryTestData
{
    /// <summary>Gets the instant every builder uses unless a test supplies one.</summary>
    public static DateTimeOffset Now { get; } = DateTimeOffset.UnixEpoch.AddHours(1);

    /// <summary>Gets the profile key every builder captures.</summary>
    public static MemoryProfileKey ProfileKey { get; } = new("memory");

    /// <summary>Gets the profile version every builder captures.</summary>
    public static MemoryProfileVersion ProfileVersion { get; } = new(1);

    /// <summary>Creates a fresh caller in one tenant.</summary>
    /// <param name="tenant">The tenant text.</param>
    /// <param name="principal">The principal text.</param>
    /// <returns>A caller whose authorization scope is exactly itself.</returns>
    public static MemoryTestOwner NewOwner(string tenant = "tenant", string principal = "principal")
    {
        var agent = new AgentId(Guid.NewGuid());
        var session = new SessionId(Guid.NewGuid());
        var run = new RunId(Guid.NewGuid());
        return Owner(agent, session, run, tenant, principal);
    }

    /// <summary>Creates a caller for explicit identities.</summary>
    /// <param name="agent">The agent.</param>
    /// <param name="session">The session.</param>
    /// <param name="run">The run.</param>
    /// <param name="tenant">The tenant text.</param>
    /// <param name="principal">The principal text.</param>
    /// <returns>A caller whose authorization scope is exactly the supplied identities.</returns>
    public static MemoryTestOwner Owner(AgentId agent, SessionId session, RunId run, string tenant = "tenant", string principal = "principal")
    {
        var identity = TestExecutionIdentity.Create(new TenantId(tenant), new PrincipalId(principal), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), run, null);
        var authorization = TestSecurityEvidence.Authorization(agent, session, correlation, identity);
        var context = new MemoryOperationContext(agent, session, identity, correlation, authorization, ProfileKey, ProfileVersion);
        return new(agent, session, run, identity, authorization, context);
    }

    /// <summary>Creates an active memory record owned by a caller.</summary>
    /// <param name="owner">The owning caller.</param>
    /// <param name="text">The body text.</param>
    /// <param name="state">The lifecycle state.</param>
    /// <param name="namespace">The namespace text.</param>
    /// <param name="shared">Whether every principal in the tenant may read it.</param>
    /// <param name="classification">The sensitivity.</param>
    /// <param name="id">The identity, or a fresh one.</param>
    /// <param name="version">The version text.</param>
    /// <returns>A valid record.</returns>
    public static DurableMemoryRecord Record(
        MemoryTestOwner owner,
        string text = "The user prefers concise answers.",
        MemoryLifecycleState state = MemoryLifecycleState.Active,
        string @namespace = "default",
        bool shared = false,
        DataClassification classification = DataClassification.Internal,
        MemoryId? id = null,
        string version = "1") => new(
            id ?? new MemoryId(Guid.NewGuid()),
            owner.AgentId,
            owner.SessionId,
            owner.RunId,
            new MemoryNamespace(@namespace),
            owner.Identity.TenantId,
            new PrincipalVisibility(owner.Identity.TenantId, owner.Identity.PrincipalId, shared),
            MemoryKind.UserPreference,
            new MemoryContent(text),
            classification,
            new Provenance("user", owner.RunId, owner.SessionId),
            new RetentionPolicy(),
            state,
            new VersionToken(version),
            Now,
            Now,
            ExtensionData.Empty);

    /// <summary>Creates a proposal from a caller.</summary>
    /// <param name="owner">The proposing caller.</param>
    /// <param name="text">The body text.</param>
    /// <param name="classification">The sensitivity.</param>
    /// <returns>A valid proposal.</returns>
    public static MemoryProposal Proposal(MemoryTestOwner owner, string text = "The user prefers concise answers.", DataClassification classification = DataClassification.Internal) => new(
        new MemoryId(Guid.NewGuid()),
        owner.Context,
        MemoryKind.UserPreference,
        new MemoryContent(text),
        classification,
        new Provenance("user", owner.RunId, owner.SessionId),
        new RetentionPolicy(),
        Now,
        ExtensionData.Empty);

    /// <summary>Creates a document version record owned by a caller.</summary>
    /// <param name="owner">The owning caller.</param>
    /// <param name="version">The source version text.</param>
    /// <param name="hash">The content hash text.</param>
    /// <param name="id">The document identity, or a fresh one.</param>
    /// <param name="shared">Whether every principal in the tenant may read it.</param>
    /// <returns>A valid record.</returns>
    public static DocumentRecord Document(MemoryTestOwner owner, string version = "v1", string hash = "sha256:aa", DocumentId? id = null, bool shared = false) => new(
        id ?? new DocumentId(Guid.NewGuid()),
        owner.AgentId,
        owner.SessionId,
        owner.RunId,
        owner.Identity.TenantId,
        new PrincipalVisibility(owner.Identity.TenantId, owner.Identity.PrincipalId, shared),
        new DocumentVersion(version),
        new ContentHash(hash),
        new DocumentMetadata("Handbook", "text/plain", null, ExtensionData.Empty),
        DataClassification.Internal,
        new Provenance("upload", owner.RunId, owner.SessionId),
        new RetentionPolicy());

    /// <summary>Creates a deterministic chunk set for a document version.</summary>
    /// <param name="record">The version's record.</param>
    /// <param name="count">The number of chunks.</param>
    /// <param name="chunker">The chunker version text.</param>
    /// <returns>Chunks with contiguous ordinals and identities derived from the version and ordinal.</returns>
    public static ImmutableArray<DocumentChunk> Chunks(DocumentRecord record, int count = 2, string chunker = "test-chunker-1")
    {
        var builder = ImmutableArray.CreateBuilder<DocumentChunk>(count);
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{record.Id}|{record.Version}|{chunker}|{ordinal}"));
            builder.Add(new DocumentChunk(
                new ChunkId(new Guid(bytes.AsSpan(0, 16))),
                record.Id,
                record.Version,
                new ChunkerVersion(chunker),
                ordinal,
                $"Chunk {ordinal} of {record.Version}",
                new ContentHash($"sha256:{ordinal:x2}")));
        }

        return builder.MoveToImmutable();
    }

    /// <summary>Creates a vector space with the given shape.</summary>
    /// <param name="key">The index key text.</param>
    /// <param name="dimensions">The vector dimensions.</param>
    /// <param name="metric">The distance metric.</param>
    /// <param name="model">The resolved model text, so tests can build incompatible spaces.</param>
    /// <returns>A valid descriptor.</returns>
    public static VectorSpaceDescriptor Space(string key = "vectors", int dimensions = 3, VectorDistanceMetric metric = VectorDistanceMetric.Cosine, string model = "embed-1") =>
        new(new VectorIndexKey(key), EmbeddingSpace(dimensions, model), metric);

    /// <summary>Creates an embedding space identity.</summary>
    /// <param name="dimensions">The vector dimensions.</param>
    /// <param name="model">The resolved model text.</param>
    /// <param name="purpose">The embedding purpose.</param>
    /// <returns>A valid identity.</returns>
    public static EmbeddingSpaceIdentity EmbeddingSpace(int dimensions = 3, string model = "embed-1", EmbeddingPurpose purpose = EmbeddingPurpose.Document) => new(
        new ProviderResponseIdentity(
            new ProviderId("test"), null, new ApiFamilyId("embeddings"), new ModelId(model), new ModelId(model), null,
            new ProviderRequestId(Guid.NewGuid().ToString("N")), null),
        dimensions,
        EmbeddingElementType.Float32,
        purpose,
        ExtensionData.Empty);

    /// <summary>Creates a vector record for a chunk.</summary>
    /// <param name="owner">The owning caller.</param>
    /// <param name="chunk">The chunk the vector derives from.</param>
    /// <param name="components">The vector components.</param>
    /// <returns>A valid vector record.</returns>
    public static VectorRecord Vector(MemoryTestOwner owner, DocumentChunk chunk, params float[] components) => new(
        chunk.Id,
        chunk.DocumentId,
        chunk.Version,
        owner.AgentId,
        new PrincipalVisibility(owner.Identity.TenantId, owner.Identity.PrincipalId),
        [.. components],
        chunk.Hash,
        chunk.Chunker,
        Now);

    /// <summary>Creates a generic single-use grant whose captured authorization is a caller's.</summary>
    /// <param name="owner">The caller.</param>
    /// <param name="audience">The audience text, or a generic test audience.</param>
    /// <returns>A valid grant that binds an arbitrary probe operation.</returns>
    public static SecurityGrant Grant(MemoryTestOwner owner, string audience = "test-store") =>
        new TestGoalGrants().Issue(
            new ComponentId(audience),
            owner.Authorization,
            SecurityOperationKind.StateRead,
            SecurityEffect.Observe,
            [new ProtectedResource(ProtectedResourceKind.ApplicationState, "probe")],
            new InputFingerprint("probe"));

    /// <summary>Creates a retrieval query for a caller.</summary>
    /// <param name="owner">The caller.</param>
    /// <param name="text">The query text.</param>
    /// <param name="budget">The requested budget, or a small default.</param>
    /// <param name="maximumClassification">The classification ceiling.</param>
    /// <param name="destination">The exposure destination, or null.</param>
    /// <returns>A valid query.</returns>
    public static RetrievalQuery Query(
        MemoryTestOwner owner,
        string text = "concise answers",
        RetrievalBudget? budget = null,
        DataClassification maximumClassification = DataClassification.Confidential,
        ModelDestination? destination = null) => new(
            new RetrievalRequestId(Guid.NewGuid()),
            owner.Context,
            new RetrievalQueryContent(text),
            RetrievalScope.Unrestricted,
            budget ?? new RetrievalBudget(10, 10_000, 2_000),
            maximumClassification,
            destination);

    /// <summary>Creates a durable-memory retrieval candidate.</summary>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="text">The candidate text.</param>
    /// <param name="score">The score.</param>
    /// <param name="source">The source key text.</param>
    /// <param name="memoryId">The memory identity, or a fresh one.</param>
    /// <param name="classification">The sensitivity.</param>
    /// <returns>A valid candidate.</returns>
    public static RetrievalCandidate Candidate(
        RetrievalRequestId requestId,
        string text = "candidate text",
        double score = 1,
        string source = "source",
        MemoryId? memoryId = null,
        DataClassification classification = DataClassification.Internal) => new(
            requestId,
            new RetrievalSourceIdentity(new RetrievalSourceKey(source), "1", 1),
            memoryId ?? new MemoryId(Guid.NewGuid()),
            null,
            null,
            new CandidateContent(text),
            new Provenance("test"),
            TrustClassification.UntrustedData,
            score,
            classification);

    /// <summary>Creates a profile snapshot with durable memory and retrieval enabled.</summary>
    /// <param name="key">The profile key text.</param>
    /// <param name="version">The profile version.</param>
    /// <param name="memoryStore">The memory store key text, or null.</param>
    /// <param name="documentStore">The document store key text, or null.</param>
    /// <param name="vectorIndexes">The vector index key texts.</param>
    /// <param name="sources">The retrieval source key texts.</param>
    /// <param name="rewriter">The captured rewriter, or null for none.</param>
    /// <param name="embedding">The embedding triple, or null.</param>
    /// <param name="reranker">The reranker triple, or null.</param>
    /// <param name="exposureAuthorization">Whether exposure authorization is required.</param>
    /// <param name="maximumClassification">The profile classification ceiling.</param>
    /// <param name="policyProfile">The policy profile key text.</param>
    /// <returns>A valid snapshot.</returns>
    public static MemoryProfileSnapshot Snapshot(
        string key = "memory",
        long version = 1,
        string? memoryStore = "memory-store",
        string? documentStore = null,
        string[]? vectorIndexes = null,
        string[]? sources = null,
        QueryRewriterReference? rewriter = null,
        EmbeddingRuntimeReference? embedding = null,
        RerankerRuntimeReference? reranker = null,
        bool exposureAuthorization = true,
        DataClassification maximumClassification = DataClassification.Confidential,
        string policyProfile = "allow") => new(
            new MemoryProfileKey(key),
            new MemoryProfileVersion(version),
            durableMemoryEnabled: memoryStore is not null,
            retrievalEnabled: true,
            queryRewritingEnabled: rewriter is not null,
            exposureAuthorization,
            memoryStore is null ? null : new MemoryStoreKey(memoryStore),
            documentStore is null ? null : new DocumentStoreKey(documentStore),
            [.. (vectorIndexes ?? []).Select(static name => new VectorIndexKey(name))],
            [.. (sources ?? ["source"]).Select(static name => new RetrievalSourceKey(name))],
            rewriter,
            new MemoryPolicyProfileKey(policyProfile),
            embedding,
            reranker,
            new RetrievalBudget(20, 262_144, 8_192),
            maximumClassification,
            new ContentHash("sha256:fingerprint"));
}
