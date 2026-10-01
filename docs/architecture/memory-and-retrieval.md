# Memory and retrieval

**Role:** Retain and retrieve durable knowledge without confusing it with
conversation history or model context.

This component follows the
[memory, retrieval, and storage boundaries](../concepts/memory-retrieval-and-storage.md):
session history remains with sessions, working context remains with context
assembly, and embedding generation remains a provider operation.

AgentKit.Memory owns the first-party memory lifecycle and retrieval pipeline.
Stores and vector integrations remain leaf packages named for their backend.
Their contracts live in AgentKit.Abstractions, so applications may replace the
pipeline or any storage axis independently.

`AgentEngine` may host many agents concurrently. Memory and retrieval never use
engine-global ambient scope: proposals, records, queries, selected stores, and
results carry typed `AgentId`, `SessionId`, operation correlation, complete
`ExecutionIdentity`, and tenant/principal visibility. Sharing a process does not
imply sharing memory.

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

public readonly record struct MemoryProfileVersion(long Value);

public readonly record struct RetrievalSourceKey(string Value);

public readonly record struct MemoryPolicyProfileKey(string Value);

public readonly record struct QueryRewriterKey(string Value);

public readonly record struct QueryRewriterVersion(string Value);
```

Memory, document, chunk, retrieval, and operation identities come from injected
generators. Time and expiry use `TimeProvider`; deterministic chunk IDs derive
from source content/version and a versioned chunker, not random ambient state.

```csharp
namespace AgentKit;

public sealed record MemoryOperationContext(
    AgentId AgentId,
    SessionId SessionId,
    ExecutionIdentity Identity,
    OperationCorrelation Correlation,
    SecurityAuthorizationContext Authorization,
    MemoryProfileKey ProfileKey,
    MemoryProfileVersion ProfileVersion);

public sealed record MemoryProposal(
    MemoryId Id,
    MemoryOperationContext Context,
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
    TenantId TenantId,
    PrincipalVisibility Visibility,
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
    MemoryOperationContext Context,
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
`MemoryOperationContext` carries the complete authenticated `ExecutionIdentity`
and the exact authorization, agent-definition, configuration, memory-profile,
and operation correlation snapshots used by the call. Construction rejects
disagreement between those values. Tenant and owner fields on durable records
are normalized routing and visibility projections; they never replace the
authenticated operation identity that authorized a read or write.

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

### Immutable profile selection and runtime capability

An agent selects one versioned memory profile when its run plan is compiled. The
profile is data; the runtime capability is an invocation-scoped bundle of the
exact services selected for that profile. This keeps ordinary constructor
injection useful while avoiding keyed-service discovery inside the retrieval
pipeline.

```csharp
namespace AgentKit;

public sealed record EmbeddingRuntimeReference(
    ComponentKey<IEmbeddingModelSelector> SelectorKey,
    ComponentKey<IEmbeddingRequestExecutor> ExecutorKey,
    EmbeddingSelectionPolicy Policy);

public sealed record RerankerRuntimeReference(
    ComponentKey<IRerankerSelector> SelectorKey,
    ComponentKey<IRerankRequestExecutor> ExecutorKey,
    RerankerSelectionPolicy Policy);

public sealed record MemoryProfileSnapshot(
    MemoryProfileKey Key,
    MemoryProfileVersion Version,
    bool DurableMemoryEnabled,
    bool RetrievalEnabled,
    bool QueryRewritingEnabled,
    bool RequireExposureAuthorization,
    MemoryStoreKey? MemoryStore,
    DocumentStoreKey? DocumentStore,
    ImmutableArray<VectorIndexKey> VectorIndexes,
    ImmutableArray<RetrievalSourceKey> RetrievalSources,
    QueryRewriterReference? QueryRewriter,
    MemoryPolicyProfileKey PolicyProfile,
    EmbeddingRuntimeReference? Embedding,
    RerankerRuntimeReference? Reranker,
    RetrievalBudget RetrievalBudget,
    DataClassification MaximumClassification,
    ContentHash ConfigurationFingerprint);

public interface IMemoryProfileRuntimeLease : IAsyncDisposable
{
    MemoryProfileSnapshot Profile { get; }
    IMemoryStore? MemoryStore { get; }
    IDocumentStore? DocumentStore { get; }
    ImmutableArray<IVectorIndex> VectorIndexes { get; }
    IRetrievalSourceSelector Sources { get; }
    IQueryRewriter? QueryRewriter { get; }
    IEmbeddingModelSelector? EmbeddingSelector { get; }
    IEmbeddingRequestExecutor? EmbeddingExecutor { get; }
    IRerankerSelector? RerankerSelector { get; }
    IRerankRequestExecutor? RerankerExecutor { get; }
    ModelCatalogSnapshot Models { get; }
    ISecurityAuthoritySelector SecurityAuthorities { get; }
    BudgetExecutionCapability Budget { get; }
    IRetrievalBudgetPolicy BudgetPolicy { get; }
    IMemoryEventDispatcher Events { get; }
}

public abstract record MemoryProfileRuntimeSelectionResult;

public enum MemoryProfileRuntimeFailureKind
{
    UnknownProfile,
    VersionMismatch,
    MissingCapability,
    InvalidComposition
}

public sealed record MemoryProfileRuntimeFailure(
    MemoryProfileRuntimeFailureKind Kind,
    string SafeMessage);

public sealed record MemoryProfileRuntimeSelected(
    IMemoryProfileRuntimeLease Runtime)
    : MemoryProfileRuntimeSelectionResult;

public sealed record MemoryProfileRuntimeUnavailable(
    MemoryProfileKey ProfileKey,
    MemoryProfileVersion ProfileVersion,
    MemoryProfileRuntimeFailure Failure)
    : MemoryProfileRuntimeSelectionResult;

public interface IMemoryProfileRuntimeSelector
{
    ValueTask<MemoryProfileRuntimeSelectionResult> SelectAsync(
        MemoryOperationContext context,
        CancellationToken cancellationToken = default);
}
```

The selector is the single engine-wide routing boundary. It validates the
requested key and version, activates the keyed scope, captures one immutable
model catalog snapshot, and returns an owned lease. A successful caller must
dispose that lease after the operation. The lease may omit embedding or
reranking collaborators only when the profile does not select that operation;
using an omitted capability returns a typed unavailable result before I/O.
Neither the selector nor the lease exposes `IServiceProvider`.

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

public interface IMemoryPolicyDispatcher
{
    ValueTask<MemoryPolicyDecision> EvaluateAsync(
        MemoryProposal proposal,
        MemoryPolicyContext context,
        CancellationToken cancellationToken = default);
}

public interface IMemoryCoordinator
{
    ValueTask<MemoryProposalResult> ProposeAsync(
        MemoryProposal proposal,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);

    ValueTask<MemoryTransitionResult> CorrectAsync(
        MemoryCorrectionRequest request,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken);

    ValueTask<MemoryDeleteResult> DeleteAsync(
        MemoryDeleteRequest request,
        HookDispatchContext? hooks,
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
    QueryRewriterDescriptor Descriptor { get; }

    ValueTask<QueryRewriteResult> RewriteAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken);
}

public sealed record QueryRewriterDescriptor(
    QueryRewriterKey Key,
    QueryRewriterVersion Version);

public sealed record QueryRewriterReference(
    QueryRewriterKey Key,
    QueryRewriterVersion Version);

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

public interface IMemoryEventDispatcher
{
    ValueTask<MemoryEventDispatchResult> PublishAsync(
        MemoryProfileKey profile,
        MemoryEvent memoryEvent,
        CancellationToken cancellationToken = default);
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
    IMemoryProfileRuntimeSelector runtimes,
    IHookDispatcher hooks,
    TimeProvider timeProvider,
    ILogger<RetrievalPipeline> logger) : IRetrievalPipeline
{
}

internal sealed class DefaultMemoryCoordinator(
    IMemoryProfileRuntimeSelector runtimes,
    IMemoryPolicyDispatcher policies,
    IIdentifierGenerator<MemoryId> memoryIds,
    IHookDispatcher hooks,
    TimeProvider timeProvider,
    ILogger<DefaultMemoryCoordinator> logger) : IMemoryCoordinator
{
}
```

Each operation asks `IMemoryProfileRuntimeSelector` for the exact profile key
and version in `MemoryOperationContext`, holds the resulting lease through
authorization, provider calls, storage, required observation, and settlement,
then disposes it. The pipeline and coordinator therefore cannot accidentally
combine one agent's source selector or query rewriter with another agent's
embedding model, security authority, budgets, store, or event dispatcher. A
profile with rewriting disabled receives no rewriter; an enabled profile
captures the selected rewriter key and version and receives that exact instance
in its lease. The operation-owned budget capability covers retrieval and any
embedding or reranking child attempts. The selected security authority issues
separate bounded grants for query, embedding egress, each source read, result
exposure, and durable mutation; no grant is reused for a different concrete
effect.

The active `HookDispatchContext` is passed separately to coordinator and
retrieval invocations. In-run callers provide their compiled hook lease;
maintenance callers may pass `null`. The injected dispatcher uses only that
lease and never selects a live/unkeyed hook profile or persists the context.

### Memory hook points

Four typed hook points, all with `FailOperation` failure policy, let an
application observe and constrain memory without replacing the coordinator or
pipeline. Their registration extensions (`AddBeforeMemoryProposalHook`,
`AddBeforeMemoryWriteHook`, `AddBeforeRetrievalHook`,
`AddBeforeRetrievalExposureHook`) live in `AgentKit.Hooks`, which owns dispatch;
the interfaces and event arguments live in `AgentKit.Abstractions`. The point
names are an addition to the specification, which fixes only that the
coordinator and pipeline accept a `HookDispatchContext?`.

| Point (`AgentHookPoints`)                                  | Kind            | Runs                                                                                        | May change                                                                    |
| ---------------------------------------------------------- | --------------- | ------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `BeforeMemoryProposal` (`agentkit.memory.proposal.before`) | ShortCircuiting | In `ProposeAsync` and `CorrectAsync`, before memory policy evaluates the proposal           | Set a `MemoryHookVeto(code, safeReason)`, which becomes a typed policy denial |
| `BeforeMemoryWrite`                                        | ShortCircuiting | After policy allowed the write and the record is built, before the write grant is requested | Set a `MemoryHookVeto`                                                        |
| `BeforeRetrieval`                                          | Mutating        | After the profile budget narrowing, before the retrieval read grant                         | Lower `MaximumItems` and `MaximumBytes` only (never raise)                    |
| `BeforeRetrievalExposure`                                  | Mutating        | After authorization, rerank, and exposure authorization, before the budget selection        | Add `ExcludedPositions` (drop candidates only; no add, reorder, or edit)      |

`CorrectAsync` dispatches the proposal and write points for the replacement
record; `DeleteAsync` dispatches none, because a deletion is never blocked by an
application hook. A hook cannot widen a budget, admit a candidate the pipeline
refused, or mint a grant: vetoes only deny, and exclusion only removes.
`MemoryHookRunner` derives a fresh per-point dispatch from the caller's context
(catalog, activation, correlation, and deadline), dispatches a point only when
the captured catalog registers it, and with no context, dispatcher, or
registration the operation runs exactly as without hooks. A hook fault, invalid
mutation, or timeout refuses the operation fail-closed with the fixed safe
message "A memory hook failed, so the operation was refused."; cancellation
propagates.

In-run retrieval reaches these hooks: the loop captures the run's hook context
when its catalog registers a retrieval point and forwards it through
`ContextAssemblyRequest.Hooks` and `ContextContributionRequest.Hooks`, and
`RetrievalContextContributor` passes it to the pipeline. The assembler never
dispatches through it. A run whose catalog registers no retrieval hook carries
no context, so retrieval behaves exactly as before.

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

public static class MemoryPolicyProfileKeys
{
    public static MemoryPolicyProfileKey FailClosed { get; } =
        new("agentkit.fail-closed");
}

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

public sealed class MemoryProfileOptions
{
    public MemoryProfileVersion Version { get; set; } = new(1);
    public bool EnableDurableMemory { get; set; }
    public bool EnableRetrieval { get; set; }
    public bool EnableQueryRewriting { get; set; }
    public bool? RequireExposureAuthorization { get; set; }
    public MemoryStoreKey? MemoryStore { get; set; }
    public DocumentStoreKey? DocumentStore { get; set; }
    public List<VectorIndexKey> VectorIndexes { get; set; } = [];
    public List<RetrievalSourceKey> RetrievalSources { get; set; } = [];
    public QueryRewriterKey? QueryRewriter { get; set; }
    public MemoryPolicyProfileKey PolicyProfile { get; set; } =
        MemoryPolicyProfileKeys.FailClosed;
    public ComponentKey<IEmbeddingModelSelector>? EmbeddingSelectorKey
        { get; set; }
    public ComponentKey<IEmbeddingRequestExecutor>? EmbeddingExecutorKey
        { get; set; }
    public List<EmbeddingModelAlias> EmbeddingModels { get; set; } = [];
    public ComponentKey<IRerankerSelector>? RerankerSelectorKey { get; set; }
    public ComponentKey<IRerankRequestExecutor>? RerankerExecutorKey
        { get; set; }
    public List<RerankerAlias> Rerankers { get; set; } = [];
    public int? MaximumRetrievedItems { get; set; }
    public int? MaximumRetrievedBytes { get; set; }
    public int? MaximumRetrievalTokens { get; set; }
    public DataClassification? MaximumClassification { get; set; }
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

        public IServiceCollection ReplaceMemoryProfile(
            MemoryProfileKey key,
            Action<MemoryProfileOptions> configure) =>
            MemoryServiceRegistration.ReplaceMemoryProfile(
                services,
                key,
                configure);

        public IServiceCollection AddMemoryStore<TStore>(MemoryStoreKey key)
            where TStore : class, IMemoryStore =>
            MemoryServiceRegistration.AddMemoryStore<TStore>(services, key);

        public IServiceCollection ReplaceMemoryStore<TStore>(MemoryStoreKey key)
            where TStore : class, IMemoryStore =>
            MemoryServiceRegistration.ReplaceMemoryStore<TStore>(services, key);

        public IServiceCollection AddDocumentStore<TStore>(DocumentStoreKey key)
            where TStore : class, IDocumentStore =>
            MemoryServiceRegistration.AddDocumentStore<TStore>(services, key);

        public IServiceCollection ReplaceDocumentStore<TStore>(
            DocumentStoreKey key)
            where TStore : class, IDocumentStore =>
            MemoryServiceRegistration.ReplaceDocumentStore<TStore>(
                services,
                key);

        public IServiceCollection AddVectorIndex<TIndex>(VectorIndexKey key)
            where TIndex : class, IVectorIndex =>
            MemoryServiceRegistration.AddVectorIndex<TIndex>(services, key);

        public IServiceCollection ReplaceVectorIndex<TIndex>(VectorIndexKey key)
            where TIndex : class, IVectorIndex =>
            MemoryServiceRegistration.ReplaceVectorIndex<TIndex>(services, key);

        public IServiceCollection AddRetrievalSource<TSource>(
            RetrievalSourceKey key)
            where TSource : class, IRetrievalSource =>
            MemoryServiceRegistration.AddRetrievalSource<TSource>(services, key);

        public IServiceCollection ReplaceRetrievalSource<TSource>(
            RetrievalSourceKey key)
            where TSource : class, IRetrievalSource =>
            MemoryServiceRegistration.ReplaceRetrievalSource<TSource>(
                services,
                key);

        public IServiceCollection AddMemoryPolicy<TPolicy>(
            MemoryPolicyRegistration registration)
            where TPolicy : class, IMemoryPolicy =>
            MemoryServiceRegistration.AddMemoryPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection ReplaceMemoryPolicy<TPolicy>(
            MemoryPolicyRegistration registration)
            where TPolicy : class, IMemoryPolicy =>
            MemoryServiceRegistration.ReplaceMemoryPolicy<TPolicy>(
                services,
                registration);

        public IServiceCollection ReplaceRetrievalPipeline<TPipeline>()
            where TPipeline : class, IRetrievalPipeline =>
            MemoryServiceRegistration.ReplaceRetrievalPipeline<TPipeline>(
                services);

        public IServiceCollection ReplaceMemoryCoordinator<TCoordinator>()
            where TCoordinator : class, IMemoryCoordinator =>
            MemoryServiceRegistration.ReplaceMemoryCoordinator<TCoordinator>(
                services);

        public IServiceCollection
            ReplaceMemoryProfileRuntimeSelector<TSelector>()
            where TSelector : class, IMemoryProfileRuntimeSelector =>
            MemoryServiceRegistration.ReplaceRuntimeSelector<TSelector>(
                services);

        public IServiceCollection AddQueryRewriter<TRewriter>(
            QueryRewriterDescriptor descriptor)
            where TRewriter : class, IQueryRewriter =>
            MemoryServiceRegistration.AddQueryRewriter<TRewriter>(
                services,
                descriptor);

        public IServiceCollection ReplaceQueryRewriter<TRewriter>(
            QueryRewriterDescriptor descriptor)
            where TRewriter : class, IQueryRewriter =>
            MemoryServiceRegistration.ReplaceQueryRewriter<TRewriter>(
                services,
                descriptor);

        public IServiceCollection ReplaceRetrievalBudgetPolicy<TPolicy>()
            where TPolicy : class, IRetrievalBudgetPolicy =>
            MemoryServiceRegistration.ReplaceBudgetPolicy<TPolicy>(services);

        public IServiceCollection ReplaceMemoryPolicyDispatcher<TDispatcher>()
            where TDispatcher : class, IMemoryPolicyDispatcher =>
            MemoryServiceRegistration.ReplacePolicyDispatcher<TDispatcher>(
                services);

        public IServiceCollection AddMemoryEventSink<TSink>(
            MemoryEventSinkRegistration registration)
            where TSink : class, IMemoryEventSink =>
            MemoryServiceRegistration.AddEventSink<TSink>(
                services,
                registration);

        public IServiceCollection ReplaceMemoryEventDispatcher<TDispatcher>()
            where TDispatcher : class, IMemoryEventDispatcher =>
            MemoryServiceRegistration.ReplaceEventDispatcher<TDispatcher>(
                services);
    }
}
```

The package-internal `MemoryServiceRegistration` helper performs registrations
without building or resolving a service provider.

`AddAgentMemory` is idempotent and `TryAdd`s singular coordinators and retrieval
pipelines, the engine-wide memory-profile runtime selector, policy and event
dispatchers, the default no-rewrite strategy, bounded budget policy, and
fail-closed memory policy. Explicit replacement methods replace singular axes or
one exact keyed implementation. Stores, indexes, retrieval sources, policy
contributors, chunkers, and event sinks are additive. Store/index keys and
source identities are unique; duplicate registration fails build unless
explicitly replaced.

The base registration is useful without inventing external facts: it enables no
durable store, vector index, retrieval source, embedding model, reranker,
credential, endpoint, persistence target, or cross-principal visibility. The
default profile version is one, proposal policy denies until an explicit policy
allows retention, durable memory and retrieval are off, query rewriting is off,
exposure authorization remains on, and retrieval bounds inherit the finite
engine ceilings shown above. Enabling an axis without naming all of its required
keyed collaborators and a classification ceiling fails composition validation.

Embedding and reranking are enabled only by a complete selected triple: selector
key, executor key, and non-empty ordered aliases. The compiled snapshot retains
all three while the runtime lease captures the matching catalog version.
Supplying only part of a triple is a build error; no global/default semantic
operation is discovered at runtime.

Backends use leaf packages such as `AgentKit.Memory.InMemory`,
`AgentKit.Memory.Sqlite`, or `AgentKit.Memory.Qdrant` and register each
supported state operation independently. The in-memory and SQLite leaves run the
same conformance suites for their shared memory and document contracts. SQLite
does not advertise vector search merely because it stores vector bytes; a
verified index implementation or extension must satisfy the vector capability
suite. `AddAgentMemory` installs no hidden durable store or vector service.
Runtime components receive typed catalogs/selectors, never the service provider
as a locator.

Catalogs, selectors, policies proven thread-safe, and store clients may be
singletons. Mutable proposal, retrieval, ranking, and budget state is run- or
operation-scoped. Runtime leases and the pipeline are scoped; a lease may hold a
keyed child scope but never outlives the operation that selected it. The
container owns store clients; enumerated pages or streams declare caller
disposal. Different agent scopes may execute concurrently. Optimistic versions,
idempotency keys, bounded batches, and cancellation define partial-failure
semantics; retries belong to the coordinator or backend named by the contract,
never both.

## Composition validation and unsupported behavior

Memory remains optional. Once a profile enables an axis, build or agent-
definition validation checks its singular runtime services, referenced keyed
stores/indexes/sources, embedding and reranking capabilities, complete vector
compatibility, security authority, hook dispatcher, `TimeProvider`, ID
generators, classification/region constraints, bounds, lifetimes, deletion
propagation, and required audit delivery.

Validation covers both graphs. Package references remain acyclic, and every
selected runtime lease must be constructible without a constructor/factory
cycle. In particular, memory profiles may consume provider, security, budget,
hook, and storage abstractions, while those components never depend back on the
memory coordinator or retrieval pipeline.

An agent without a memory profile has no durable-memory or retrieval capability.
Missing stores, incompatible vector spaces, unsupported modality, unavailable
deletion guarantees, or excessive classification return typed unavailable or
policy results before I/O. Cross-agent, tenant, principal, or namespace access;
missing or stale grants; untrusted instructions; and unavailable exposure audit
fail closed. The pipeline never silently drops provenance, stringifies
unsupported media, mixes vector spaces, or treats provider output as authority.

Every selected store has an explicit persistence target and descriptor.
In-memory storage is process-local. SQLite may advertise durable local atomic
source publication, tombstones, and restart recovery only for operations its
tests prove; it does not imply distributed indexing, remote replication, or an
atomic transaction with artifact or session storage.

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

## Publication, deletion, and exposure consistency

A source update creates a complete versioned chunk set. Indexing may stage it
incrementally, but an atomic active-version pointer switches retrieval only once
the declared set is ready. Query results carry source/version and index
watermarks; stale index hits are filtered against the authoritative active
source state before reranking or model exposure. Failed indexing leaves the
prior active version intact and a recoverable intent, rather than a mixed
version.

Deletion commits an authoritative tombstone before asynchronous removal from
indexes, caches, and artifact stores. New retrieval and exposure reject that
source version even while physical cleanup is pending. A deletion receipt
separates logical invisibility from physical purge and names the pending stores
without returning deleted content. Tombstones and active-version evidence must
outlive stale indexes, caches, and permitted backup replay; restoring a backup
cannot resurrect deleted data for retrieval.

An exposure decision binds the selected source versions and deletion/revocation
generation. Before egress, a stale generation causes revalidation or context
rebuild; an unavailable required check fails closed. Already transmitted data
cannot be recalled. The deletion contract states that limit, plus retention,
backup, and externally owned-content constraints. Eventual physical cleanup is
never advertised as immediate erasure everywhere.

## Implementation notes and recorded deviations

The shapes above are normative and minimal. The landed implementation refines
them as follows; each refinement narrows a contract or names a mechanism the
minimal shape left open, and none widens authority.

### Packages and dependency direction

- `AgentKit.Abstractions` holds every memory, document, vector, retrieval,
  policy, event, profile-runtime, and document-lifecycle contract.
- `AgentKit.Memory` is the behavioral runtime. It references only Abstractions
  and Observability and never references sessions, providers, or stores.
- `AgentKit.Memory.InMemory`, `.Sqlite`, and `.Json` are store leaves. They
  share one pure planner and state model compiled from the source-only folders
  `AgentKit.Memory.Storage.Shared` and `AgentKit.Memory.Storage.Durable` into
  each assembly as internal code, so the three adapters cannot drift. These
  folders are not packages and register nothing. All adapters run the same three
  conformance suites (memory store, document store, vector index).
- `AgentKit.Context.Retrieval` is a leaf over `AgentKit.Context` because
  contributor registration needs the context package. It depends on Abstractions
  only for the retrieval pipeline and profile catalog.
- The facade validates selected profiles through Abstractions types only and
  never references `AgentKit.Memory`.

### Contracts

- Result types are sealed records with static factories rather than an abstract
  base with sealed subtypes; `MemoryStoreFailure(Kind, SafeMessage)` is the one
  typed refusal shared by the memory, document, and vector families.
- `IMemoryStore` adds `ListAsync`; `IDocumentStore` adds `ActivateAsync`, the
  atomic active-version pointer switch; `IVectorIndex` adds `SecurityAudience`,
  `IsDurable`, and `ApproximateSearch`.
- `IMemoryCoordinator` and `IDocumentLifecycleCoordinator` take command values
  that carry the captured `MemoryOperationContext` (and so the captured
  authorization), not a grant. The coordinators obtain their own single-use
  grant for each store call from the authority the captured authorization names.
  Stores take request values that carry the exact `SecurityGrant`.
- `IDocumentLifecycleCoordinator` is an addition to the minimal shape. It owns
  the stage, embed, index, activate sequence and the deletion propagation that
  the stores cannot perform across families.
- `HookDispatchContext?` is a parameter of the coordinator and pipeline
  (maintenance callers pass `null`), as in the goals runtime; see
  [Memory hook points](#memory-hook-points).
- `DataClassification` is the existing generic ordered sensitivity. A profile
  and a query each carry a ceiling; a query ceiling above the profile ceiling is
  refused, never lowered.
- `EmbeddingSpaceCompatibility.IsSameVectorSpaceAs` compares the vector space
  and ignores per-response identifiers, purpose, alias, and extension data, so
  query and document embeddings of one model are comparable while different
  spaces are never mixed on dimension alone.
- Deletion receipts report store-wide, monotonically increasing generations.
  Purge replaces a record's body with the purged marker and clears write
  receipts; reads of a deleted record return a tombstone value, never the body.
  Vector deletion receipts are retained in a bounded window of 4096.

### Fail-closed defaults

- The default policy profile is `agentkit.fail-closed` and the default
  acceptance mode is `RequireExplicitPolicyAllow`: a proposal is retained only
  when a registered policy explicitly allows it, any denial wins, and a policy
  that throws, is unavailable, or returns an unknown decision refuses the
  proposal. `AllowUnlessPolicyDenies` is an explicit engine option.
- Exposure authorization (an egress grant per candidate) is required by default.
  A profile may not disable it while the engine requires it.
- First-party retrieval sources are never installed implicitly. An application
  registers `AddDurableMemoryRetrievalSource` or `AddDocumentRetrievalSource`
  and names the key in a profile. Embedding and reranker components are keyed
  and required by name; there is no global fallback.
- A source failure degrades only that source; the pipeline fails when every
  selected source fails. A query embedding that cannot be produced removes
  embedding-dependent sources rather than querying the wrong space. A reranker
  that is unavailable keeps the original ranking. An exposure check that cannot
  be evaluated, or a required event sink that cannot record the retrieval, fails
  the retrieval and exposes nothing.

### Retrieval

- Candidates are untrusted data. The keyword source scores the fraction of
  distinct query terms a record contains; the document source searches only
  indexes in the query embedding's vector space and resolves each hit against
  the authoritative document state.
- Stale candidates are dropped before reranking: a durable-memory hit must still
  be active, unexpired, and textually identical to the stored record, and a
  document hit must belong to the active version's chunk set. The summary counts
  stale, unauthorized, duplicate, budget-omitted, and unavailable-source
  omissions.
- The default budget policy stops at the first candidate that would exceed the
  item, byte, or estimated-token ceiling; it never skips ahead to a smaller
  candidate, so ordering stays stable.
- The retrieval context contributor queries with the latest user message under
  the request's captured authorization and correlation, so the memory context
  always agrees with the authorization that was issued for that request. A
  refused retrieval contributes nothing and a content-free warning diagnostic.

### Documents, chunking, and deletion

- `DeterministicTextChunker` splits on paragraph boundaries and hard-splits an
  over-long paragraph. Chunk identity is a SHA-256 derived value over document
  id, version, chunker version, ordinal, and content hash.
- Publication stages the version inactive, embeds the chunks under an egress
  grant, upserts vectors only into indexes in the embedding's vector space, and
  switches the active pointer with the previously observed active version as the
  expected value. A failure before the switch leaves the previous version
  active; replaying a publication that is already active with identical content
  is idempotent, and with different content is a conflict. After the switch the
  superseded version's vectors are removed on a best-effort basis.
- Deletion commits the tombstone, removes the receipt's chunk vectors from every
  profile index, and purges the body only when every index is clean. Stores
  whose cleanup failed are named in the receipt's pending stores. Already
  transmitted data cannot be recalled, and pending physical cleanup is never
  reported as immediate erasure.

### Storage adapters

- The in-memory adapters are explicitly ephemeral. SQLite memory and document
  stores are durable local stores; the SQLite vector index is an exact scan and
  advertises `ApproximateSearch=false`. It makes no claim of distributed
  indexing or cross-store atomicity.
- The Json adapters hold an advisory exclusive lock, flush every acknowledged
  record, and recover a torn trailing append. The Json document store writes one
  whole document per log line and suits modest documents.

### Composition validation

The facade's memory validator proves, for every definition that selects a memory
profile, that the profile compiled and is published and that the singular
coordinator, pipeline, catalog, runtime selector, dispatchers, budget policy,
store selector, security authority selector, budget authority, and
`TimeProvider` are registered. It also proves that each store, index, source,
rewriter, embedding, and reranker component the profile names is registered
under exactly those keys, that named vector indexes come with an embedding
selection, and that a model catalog exists when embedding or reranking is named.
It reads descriptors and the compiled catalog only and never activates a store,
index, or source. A profile that fails to compile is reported as a diagnostic
rather than an exception. It does not prove memory-policy behavior or
vector-space compatibility, which are checked before I/O at publication and
retrieval.

## Related concept specifications

- [Memory, retrieval, and storage](../concepts/memory-retrieval-and-storage.md)
- [Context assembly and instructions](../concepts/context-assembly-and-instructions.md)
- [Model providers and capabilities](../concepts/model-providers-and-capabilities.md)
