# Memory and retrieval

**Role:** Retain and retrieve durable knowledge without confusing it with
conversation history or model context.

This component follows the
[memory, retrieval, and storage boundaries](../concepts/memory-retrieval-and-storage.md):
session history remains with sessions, working context remains with context
assembly, and embedding generation remains a provider operation.

AgentKit.Memory will contain the first-party memory lifecycle and retrieval
pipeline when that subsystem is implemented. Stores and vector integrations
remain leaf packages named for their backend. Their contracts live in
AgentKit.Abstractions, so applications may replace the pipeline or any storage
axis independently.

`AgentEngine` may host many agents concurrently. Memory and retrieval never use
engine-global ambient scope: proposals, records, queries, selected stores, and
results carry typed `AgentId`, `SessionId`, `RunId`, and `OperationId` plus
tenant/principal visibility. Sharing a process does not imply sharing memory.

## Normative minimal contract shape

These C# 14 shapes are normative and minimal rather than an exhaustive API
listing. Each named type lives in its own file in AgentKit.Abstractions. Shared
`AgentId`, `SessionId`, `RunId`, `OperationId`,
`IIdentifierGenerator<TIdentifier>`, `VersionToken`, and security contracts are
reused.

```csharp
namespace AgentKit;

public readonly record struct MemoryId(Guid Value);

public readonly record struct MemoryStoreKey(string Value);

public readonly record struct DocumentId(Guid Value);

public readonly record struct DocumentStoreKey(string Value);

public readonly record struct ChunkId(Guid Value);

public readonly record struct VectorIndexKey(string Value);

public readonly record struct RetrievalRequestId(Guid Value);

public readonly record struct MemoryProfileKey(string Value);
```

Memory, document, chunk, retrieval, and operation identities come from injected
generators. Time and expiry use `TimeProvider`; deterministic chunk IDs derive
from source content/version and a versioned chunker, not random ambient state.

```csharp
namespace AgentKit;

public sealed record MemoryProposal(
    MemoryId Id,
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    OperationId OperationId,
    TenantId TenantId,
    PrincipalId PrincipalId,
    MemoryKind Kind,
    MemoryContent Content,
    DataClassification Classification,
    Provenance Provenance,
    RetentionPolicy Retention,
    DateTimeOffset ProposedAt,
    ExtensionData Extensions);

public sealed record DurableMemoryRecord(
    MemoryId Id,
    AgentId AgentId,
    SessionId SourceSessionId,
    RunId SourceRunId,
    MemoryNamespace Namespace,
    TenantId TenantId,
    PrincipalVisibility Visibility,
    MemoryKind Kind,
    MemoryContent Content,
    DataClassification Classification,
    Provenance Provenance,
    RetentionPolicy Retention,
    MemoryLifecycleState State,
    VersionToken Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ExtensionData Extensions);

public sealed record DocumentRecord(
    DocumentId Id,
    AgentId AgentId,
    SessionId SourceSessionId,
    RunId SourceRunId,
    DocumentVersion Version,
    ContentHash ContentHash,
    DocumentMetadata Metadata,
    DataClassification Classification,
    Provenance Provenance,
    RetentionPolicy Retention);

public sealed record VectorSpaceDescriptor(
    VectorIndexKey IndexKey,
    EmbeddingSpaceIdentity EmbeddingSpace,
    VectorDistanceMetric DistanceMetric);

public sealed record RetrievalQuery(
    RetrievalRequestId Id,
    AgentId AgentId,
    SessionId SessionId,
    RunId RunId,
    OperationId OperationId,
    TenantId TenantId,
    PrincipalId PrincipalId,
    MemoryProfileKey ProfileKey,
    RetrievalQueryContent Query,
    RetrievalScope Scope,
    RetrievalBudget Budget,
    DataClassification MaximumClassification,
    ModelDestination? ExposureDestination);

public sealed record RetrievalCandidate(
    RetrievalRequestId RequestId,
    RetrievalSourceIdentity Source,
    MemoryId? MemoryId,
    DocumentId? DocumentId,
    ChunkId? ChunkId,
    CandidateContent Content,
    Provenance Provenance,
    TrustClassification Trust,
    double Score,
    DataClassification Classification);
```

Candidates are data, never instructions. IDs and provenance survive rewriting,
reranking, deduplication, trimming, and later context-manifest selection.

### Storage discovery, selection, and state

```csharp
namespace AgentKit;

public interface IMemoryStoreCatalog
{
    ImmutableArray<MemoryStoreDescriptor> GetDescriptors();
}

public interface IMemoryStoreSelector
{
    ValueTask<MemoryStoreSelectionResult> SelectAsync(
        MemoryStoreSelectionRequest request,
        CancellationToken cancellationToken);
}

public interface IMemoryStore
{
    MemoryStoreDescriptor Descriptor { get; }

    ValueTask<MemoryWriteResult> WriteAsync(
        MemoryWriteRequest request,
        CancellationToken cancellationToken);

    ValueTask<MemoryReadResult> ReadAsync(
        MemoryReadRequest request,
        CancellationToken cancellationToken);

    ValueTask<MemoryTransitionResult> TransitionAsync(
        MemoryTransitionRequest request,
        CancellationToken cancellationToken);

    ValueTask<MemoryDeleteResult> DeleteAsync(
        MemoryDeleteRequest request,
        CancellationToken cancellationToken);
}

public interface IDocumentStore
{
    DocumentStoreDescriptor Descriptor { get; }

    ValueTask<DocumentWriteResult> WriteAsync(
        DocumentWriteRequest request,
        CancellationToken cancellationToken);

    ValueTask<DocumentReadResult> ReadAsync(
        DocumentReadRequest request,
        CancellationToken cancellationToken);

    ValueTask<DocumentDeleteResult> DeleteAsync(
        DocumentDeleteRequest request,
        CancellationToken cancellationToken);
}

public interface IVectorIndex
{
    VectorSpaceDescriptor VectorSpace { get; }

    ValueTask<VectorUpsertResult> UpsertAsync(
        VectorUpsertRequest request,
        CancellationToken cancellationToken);

    ValueTask<VectorSearchResult> SearchAsync(
        VectorSearchRequest request,
        CancellationToken cancellationToken);

    ValueTask<VectorDeleteResult> DeleteAsync(
        VectorDeleteRequest request,
        CancellationToken cancellationToken);
}
```

Memory, document, and vector stores are independent additive keyed state axes.
Their request records include the agent/session/run/operation address,
optimistic version or idempotency key, and a bounded `SecurityGrant`. Each store
validates that grant immediately before the read or mutation. A matching vector
dimension alone is not compatibility; the complete `VectorSpaceDescriptor` must
match before contacting an index.

### Policy, retrieval execution, and observation

```csharp
namespace AgentKit;

public interface IMemoryPolicy
{
    ValueTask<MemoryPolicyDecision> EvaluateAsync(
        MemoryProposal proposal,
        MemoryPolicyContext context,
        CancellationToken cancellationToken);
}

public interface IMemoryCoordinator
{
    ValueTask<MemoryProposalResult> ProposeAsync(
        MemoryProposal proposal,
        CancellationToken cancellationToken);

    ValueTask<MemoryTransitionResult> CorrectAsync(
        MemoryCorrectionRequest request,
        CancellationToken cancellationToken);

    ValueTask<MemoryDeleteResult> DeleteAsync(
        MemoryDeleteRequest request,
        CancellationToken cancellationToken);
}

public interface IRetrievalSource
{
    RetrievalSourceDescriptor Descriptor { get; }

    ValueTask<RetrievalSourceResult> SearchAsync(
        RetrievalSourceRequest request,
        CancellationToken cancellationToken);
}

public interface IRetrievalSourceSelector
{
    ValueTask<RetrievalSourceSelectionResult> SelectAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken);
}

public interface IQueryRewriter
{
    ValueTask<QueryRewriteResult> RewriteAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken);
}

public interface IRetrievalPipeline
{
    Task<RetrievalResult> RetrieveAsync(
        RetrievalQuery query,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);
}

public interface IMemoryEventSink
{
    ValueTask PublishAsync(
        MemoryEvent memoryEvent,
        CancellationToken cancellationToken);
}
```

Policy decides whether a proposal may become durable state; it does not write.
The coordinator validates, authorizes, and delegates state mutation. Retrieval
sources discover/search candidates, the selector chooses configured sources, the
query rewriter changes only query content with provenance, and the pipeline
coordinates authorization, embedding, search, reranking, deduplication,
per-candidate exposure authorization, and budgets. Event sinks observe immutable
transitions and cannot affect policy or ranking.

AgentKit.Memory supplies sealed coordinator, store catalog/selector, retrieval
source selector, and pipeline classes. Its dependencies stay explicit:

```csharp
namespace AgentKit.Memory;

internal sealed class RetrievalPipeline(
    IRetrievalSourceSelector sourceSelector,
    IQueryRewriter queryRewriter,
    IEmbeddingModelSelector embeddingSelector,
    IEmbeddingRequestExecutor embeddingExecutor,
    IRerankerSelector rerankerSelector,
    IRerankRequestExecutor rerankExecutor,
    ISecurityAuthority securityAuthority,
    IRetrievalBudgetPolicy budgetPolicy,
    IHookDispatcher hooks,
    IEnumerable<IMemoryEventSink> eventSinks,
    TimeProvider timeProvider) : IRetrievalPipeline
{
}
```

There is no all-purpose `IMemory` and no mandatory store or pipeline base class.
Storage backends and retrieval sources implement their narrow contracts
directly. A leaf integration may provide a base class only for proven common
pagination, batching, or vector-protocol mechanics.

## Configuration and dependency injection

Agent definitions select a `MemoryProfileKey` that names memory/document/vector
stores, retrieval sources, embedding and reranking operations, budgets,
classification ceiling, and lifecycle policy. Host options are ceilings; per-run
overrides may narrow scope or budgets but never widen visibility or
classification.

```csharp
namespace AgentKit.Memory;

public sealed class AgentMemoryOptions
{
    public MemoryAcceptanceMode AcceptanceMode { get; set; } =
        MemoryAcceptanceMode.RequireExplicitPolicyAllow;
    public int MaximumRetrievedItems { get; set; } = 20;
    public int MaximumRetrievedBytes { get; set; } = 262_144;
    public int MaximumRetrievalTokens { get; set; } = 8_192;
    public bool EnableQueryRewriting { get; set; }
    public bool RequireExposureAuthorization { get; set; } = true;
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentMemory(
            Action<AgentMemoryOptions>? configure = null) =>
            MemoryServiceRegistration.AddAgentMemory(
                services,
                configure);

        public IServiceCollection AddMemoryProfile(
            MemoryProfileKey key,
            Action<MemoryProfileOptions> configure) =>
            MemoryServiceRegistration.AddMemoryProfile(services, key, configure);

        public IServiceCollection AddMemoryStore<TStore>(MemoryStoreKey key)
            where TStore : class, IMemoryStore =>
            MemoryServiceRegistration.AddMemoryStore<TStore>(services, key);

        public IServiceCollection AddDocumentStore<TStore>(DocumentStoreKey key)
            where TStore : class, IDocumentStore =>
            MemoryServiceRegistration.AddDocumentStore<TStore>(services, key);

        public IServiceCollection AddVectorIndex<TIndex>(VectorIndexKey key)
            where TIndex : class, IVectorIndex =>
            MemoryServiceRegistration.AddVectorIndex<TIndex>(services, key);

        public IServiceCollection AddRetrievalSource<TSource>()
            where TSource : class, IRetrievalSource =>
            MemoryServiceRegistration.AddRetrievalSource<TSource>(services);

        public IServiceCollection AddMemoryPolicy<TPolicy>(
            MemoryPolicyRegistration registration)
            where TPolicy : class, IMemoryPolicy =>
            MemoryServiceRegistration.AddMemoryPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection ReplaceRetrievalPipeline<TPipeline>()
            where TPipeline : class, IRetrievalPipeline =>
            MemoryServiceRegistration.ReplaceRetrievalPipeline<TPipeline>(
                services);
    }
}
```

The package-internal `MemoryServiceRegistration` helper performs registrations
without building or resolving a service provider.

`AddAgentMemory` is idempotent and `TryAdd`s singular coordinators and retrieval
pipelines, engine-wide catalogs and selectors, the default no-rewrite strategy,
budget policy, and fail-closed memory policy. Explicit replacement methods
replace singular axes. Stores, indexes, retrieval sources, policy contributors,
chunkers, and event sinks are additive. Store/index keys and source identities
are unique; duplicate registration fails build unless explicitly replaced.

Backends use leaf packages such as `AgentKit.Memory.Sqlite` or
`AgentKit.Memory.Qdrant` and register each supported state operation
independently. `AddAgentMemory` installs no hidden durable store or vector
service. Runtime components receive typed catalogs/selectors, never the service
provider as a locator.

Catalogs, selectors, policies proven thread-safe, and store clients may be
singletons. Mutable proposal, retrieval, ranking, and budget state is run- or
operation-scoped. The container owns store clients; enumerated pages or streams
declare caller disposal. Different agent scopes may execute concurrently.
Optimistic versions, idempotency keys, bounded batches, and cancellation define
partial-failure semantics; retries belong to the coordinator or backend named by
the contract, never both.

## Composition validation and unsupported behavior

Memory remains optional. Once a profile enables an axis, build or agent-
definition validation checks its singular runtime services, referenced keyed
stores/indexes/sources, embedding and reranking capabilities, complete vector
compatibility, security authority, hook dispatcher, `TimeProvider`, ID
generators, classification/region constraints, bounds, lifetimes, deletion
propagation, and required audit delivery.

An agent without a memory profile has no durable-memory or retrieval capability.
Missing stores, incompatible vector spaces, unsupported modality, unavailable
deletion guarantees, or excessive classification return typed unavailable or
policy results before I/O. Cross-agent, tenant, principal, or namespace access;
missing or stale grants; untrusted instructions; and unavailable exposure audit
fail closed. The pipeline never silently drops provenance, stringifies
unsupported media, mixes vector spaces, or treats provider output as authority.

## Durable memory

A model may propose a memory, but it cannot authorize retention. Memory policy
validates provenance, scope, sensitivity, contradiction, and classification
before accepting it. Records distinguish verified facts, preferences,
instructions, decisions, summaries, and uncertain claims.

Corrections, expiry, rejection, and deletion are explicit versioned transitions.
They do not rewrite unrelated history. Every record carries tenant and principal
visibility, namespace, source version, classification, retention, timestamps,
and optimistic version.

## Documents and vectors

Document storage preserves source material, metadata, authorization, versions,
and content identity. Deterministic chunk identity includes source and chunker
versions so updates cannot leave stale and current chunks active as one source.
Large or binary source bytes may live in [artifact storage](artifacts.md), but
memory owns document/chunk semantics and stores only the authorized immutable
reference. Artifact storage does not become an all-purpose memory service.

Each vector records its embedding provider, model revision, dimensions,
modality, normalization, distance compatibility, source hash, and chunk
identity. The vector index rejects incompatible queries before search.
Re-embedding is a versioned migration to a new compatible index followed by an
atomic alias or pointer switch.

## Retrieval

The retrieval pipeline authorizes the query and candidate scope, optionally
rewrites the query, selects sources, searches with stable filters, reranks and
deduplicates, authorizes each result for model exposure, applies item and token
budgets, and returns typed candidates with provenance.

Retrieval is not
[context assembly](../concepts/context-assembly-and-instructions.md). It
proposes authorized candidates; the context component decides which candidates
fit and records the final manifest. Retrieved content remains untrusted data and
cannot become system instruction.

## Storage and privacy

Memory, document, and vector stores define their own consistency, pagination,
idempotency, retention, deletion, and failure behavior. Database and provider
SDK types remain in integrations.

Classification controls encryption, region, logging, and model exposure.
Deletion propagates through documents, chunks, vectors, caches, and retrieval
results, with auditable tombstones where immediate removal is impossible.

## Related concept specifications

- [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)
- [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)
- [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)
