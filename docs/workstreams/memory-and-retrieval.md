# WS14: Memory and retrieval

Goal: durable memory, source documents, vector indexes, policy-governed proposal
and correction, a retrieval pipeline (authorize → rewrite → select → search →
rerank → dedupe → expose-authorize → budget), provenance-carrying context
contribution, chunking with source integrity, and deletion with tombstones and
purge receipts — across InMemory, Sqlite, and Json adapters.

All chunks have landed. The memory, document, vector, and retrieval contracts
live in `AgentKit.Abstractions`; `AgentKit.Memory` is the behavioral runtime;
the InMemory, Sqlite, and Json leaves share one planner and run three shared
conformance suites; `AgentKit.Context.Retrieval` contributes retrieved data to
context assembly; and the facade validator and `AgentKit.Simple` select a memory
profile per agent.

Owning documents:
[Memory and retrieval](../architecture/memory-and-retrieval.md),
[Memory, retrieval, storage](../concepts/memory-retrieval-and-storage.md).

## Progress

- [x] WS14-C1 classification, provenance, identities
- [x] WS14-C2 `MemoryOperationContext`, records, `IMemoryStore` family
- [x] WS14-C3 document contracts
- [x] WS14-C4 vector contracts
- [x] WS14-C5 policy, coordinator, retrieval, profile runtime, events
- [x] WS14-C6 conformance suites
- [x] WS14-C7 `AgentKit.Memory.InMemory`
- [x] WS14-C8a `AgentKit.Memory` runtime
- [x] WS14-C8b `DefaultMemoryCoordinator`
- [x] WS14-C8c `RetrievalPipeline`
- [x] WS14-C9 `AgentKit.Memory.Sqlite`
- [x] WS14-C10 `AgentKit.Memory.Json`
- [x] WS14-C11 retrieval context contributor
- [x] WS14-C12 chunking, source integrity, deletion propagation
- [x] WS14-C13 definition key, validator, Simple, documentation

## Verified current state

| Item                                                                                                                                        | State   | Evidence                                                                                                       |
| ------------------------------------------------------------------------------------------------------------------------------------------- | ------- | -------------------------------------------------------------------------------------------------------------- |
| Identities, enums, provenance, retention, visibility, destination; memory, document, vector, retrieval, policy, event, lifecycle contracts  | LANDED  | `src/AgentKit.Abstractions/{Memory,Documents,Vectors,Retrieval,Identity}/`                                     |
| Conformance suites: memory store, document store, vector index                                                                              | LANDED  | `tests/AgentKit.Conformance/{MemoryStore,DocumentStore,VectorIndex}ConformanceTests.cs`                        |
| Stores: InMemory, Sqlite (exact-scan vectors), Json                                                                                         | LANDED  | `src/AgentKit.Memory.{InMemory,Sqlite,Json}`, shared `AgentKit.Memory.Storage.{Shared,Durable}` source folders |
| Memory runtime (profiles, selector and lease, policy and event dispatch, coordinator, pipeline, budget policy, chunker, lifecycle, sources) | LANDED  | `src/AgentKit.Memory/`                                                                                         |
| Retrieval context contributor                                                                                                               | LANDED  | `src/AgentKit.Context.Retrieval/`                                                                              |
| `MemoryProfileKey? MemoryProfile` on `AgentOptionalCapabilitySelection`                                                                     | EXISTED | `Abstractions/Composition/AgentOptionalCapabilitySelection.cs`                                                 |
| Memory composition validator                                                                                                                | LANDED  | `src/AgentKit/MemoryCompositionValidator.cs`, wired from `AgentCompositionValidator`                           |
| `WithMemory` sugar                                                                                                                          | LANDED  | `src/AgentKit.Simple/AgentEngineBuilderExtensions.cs`                                                          |
| Observability                                                                                                                               | LANDED  | `memory.*` and `retrieval.*` activities and bounded metrics in `AgentKit.Observability`; event IDs 32000-32401 |

## Hidden prerequisites

1. WS7 semantic-operation contracts (`IEmbeddingModelSelector`,
   `IEmbeddingRequestExecutor`, `IRerankerSelector`, `IRerankRequestExecutor`,
   selection policies, `RerankerAlias`) are referenced by
   `EmbeddingRuntimeReference`, `RerankerRuntimeReference`, and the profile
   lease; land WS7 abstractions first or split those types into a post-WS7
   chunk.
2. WS9 `IContextContributor` for C11.
3. `HookDispatchContext` (WS2) on `IMemoryCoordinator` and `IRetrievalPipeline`;
   omit.
4. A generic `DataClassification` replaces the artifact-local enum; introduced
   here, migrated by WS15-C2.
5. `SecurityOperationKind` has no memory kind; reuse `StateMutation`/
   `StateRead` or add `Memory` additively.
6. WS7-C7 grows `EmbeddingSpaceIdentity`; key vectors on the full identity
   afterward.

## Spec coverage

| Contract                                                                                                                                                                                                                                                                                                                                                                                                       | Spec                             |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------- |
| `MemoryOperationContext`, `MemoryProposal`, `DurableMemoryRecord`, `DocumentRecord`, `VectorSpaceDescriptor`, `RetrievalQuery`, `RetrievalCandidate`                                                                                                                                                                                                                                                           | `memory-and-retrieval.md:64-145` |
| memory store catalog/selector, `IMemoryStore`, `IDocumentStore`, `IVectorIndex`                                                                                                                                                                                                                                                                                                                                | `:158-227`                       |
| runtime references, `MemoryProfileSnapshot`, lease, runtime selection, `IMemoryProfileRuntimeSelector`                                                                                                                                                                                                                                                                                                         | `:244-325`                       |
| policy and dispatcher, coordinator, retrieval source and selector, query rewriter, pipeline, event sink and dispatcher                                                                                                                                                                                                                                                                                         | `:337-429`                       |
| `RetrievalPipeline`, `DefaultMemoryCoordinator` ctors                                                                                                                                                                                                                                                                                                                                                          | `:442-462`                       |
| policy profile keys, options, profile options, DI                                                                                                                                                                                                                                                                                                                                                              | `:496-673`                       |
| `DataClassification`, `Provenance`, `TrustClassification`, `RetentionPolicy`, `MemoryNamespace`, `PrincipalVisibility`, `VectorDistanceMetric`, `ModelDestination`, `MemoryKind`, `MemoryLifecycleState`, `MemoryContent`, `DocumentMetadata`, `CandidateContent`, `RetrievalSourceIdentity`, `RetrievalScope`, `RetrievalBudget`, all request/result bodies, `MemoryEvent`, `IRetrievalBudgetPolicy`, chunker | NO-SPEC                          |
| deletion receipt semantics                                                                                                                                                                                                                                                                                                                                                                                     | prose `:811-824`; NO-SPEC shape  |

## Chunks

### WS14-C1: Classification, provenance, identities

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: `Identity/MemoryId`, `MemoryStoreKey`, `DocumentId`,
  `DocumentStoreKey`, `ChunkId`, `VectorIndexKey`, `RetrievalRequestId`,
  `MemoryProfileVersion`, `RetrievalSourceKey`, `MemoryPolicyProfileKey`,
  `QueryRewriterKey`, `QueryRewriterVersion`, `DocumentVersion`,
  `MemoryNamespace`; `Memory/DataClassification`, `Provenance`,
  `TrustClassification`, `RetentionPolicy`, `PrincipalVisibility`,
  `VectorDistanceMetric`, `ModelDestination`, `MemoryKind`,
  `MemoryLifecycleState`; identity conformance tests. Snapshot: Abstractions.

- Landed: identity types were already present; added `ChunkerVersion`, tests for
  every identity and value type in `Abstractions.Tests`, and widened
  `DataClassification` to a generic ordered sensitivity (the artifact enum is
  reused, not duplicated).

### WS14-C2: `MemoryOperationContext`, records, `IMemoryStore` family

- Depends on: C1. Risk: ADDITIVE. Size: M.
- Deliverables: `MemoryOperationContext` (validates identity/authorization
  agreement), `MemoryContent`, `MemoryProposal`, `DurableMemoryRecord`,
  `MemoryStoreDescriptor`, catalog and selector, selection request/result,
  `IMemoryStore`, write/read/transition/delete requests and results,
  `MemoryDeletionReceipt` separating logical and physical deletion; all carrying
  `SecurityGrant`.

- Landed: `MemoryOperationContext`, `MemoryContent`, `MemoryProposal` (init-only
  `Namespace` and `ShareWithTenant`), `DurableMemoryRecord`, store descriptor,
  catalog, selector, `IMemoryStore` (adds `ListAsync`), request/result families
  with static factories, `MemoryStoreFailure`, `MemoryTombstone`, and
  `MemoryDeletionReceipt`; every request carries the exact `SecurityGrant`.

### WS14-C3: Document contracts

- Depends on: C1. Risk: ADDITIVE. Size: S.
- Deliverables: `DocumentRecord`, `DocumentMetadata`, `DocumentStoreDescriptor`,
  `IDocumentStore`, write/read/delete requests and results, `DocumentChunk`,
  `ChunkerVersion`, `IDocumentChunker`; bytes via `ArtifactReference`.

- Landed: document contracts including `IDocumentStore.ActivateAsync` (stage,
  then switch the active pointer) and `DocumentDeletionReceipt` naming chunk ids
  and pending stores; bytes stay behind `ArtifactReference`.

### WS14-C4: Vector contracts

- Depends on: C1, WS7-C7. Risk: ADDITIVE. Size: S.
- Deliverables: `VectorSpaceDescriptor`, `IVectorIndex`, upsert/search/delete
  requests and results, `VectorRecord`; incompatible space descriptor rejected
  before search.

- Landed: vector contracts; `IVectorIndex` adds `SecurityAudience`, `IsDurable`,
  and `ApproximateSearch`; `EmbeddingSpaceCompatibility.IsSameVectorSpaceAs`
  ignores per-response ids, purpose, alias, and extensions so a dimension match
  alone never mixes spaces.

### WS14-C5: Policy, coordinator, retrieval, profile runtime, events

- Depends on: C2–C4; WS7 for the two runtime references. Risk: ADDITIVE. Size:
  L.
- Deliverables: `IMemoryPolicy`, dispatcher, context, decision, registration,
  `IMemoryCoordinator`, proposal result, correction request, `RetrievalQuery`,
  query content, scope, budget, `RetrievalCandidate`, candidate content, source
  identity, `RetrievalResult`, `IRetrievalSource` and descriptor,
  request/result, source selector and result, `IQueryRewriter` with descriptor
  and reference, rewrite result, `IRetrievalPipeline`, `IRetrievalBudgetPolicy`,
  event sink and dispatcher, `MemoryEvent`, dispatch result, registration,
  `MemoryProfileSnapshot`, runtime references, profile lease and selector,
  selection results.

- Landed: policy, coordinator, retrieval, profile-runtime, and event contracts;
  `IDocumentLifecycleCoordinator` with publish and removal commands and results
  was added for C12. `HookDispatchContext` is omitted as in WS13.

### WS14-C6: Conformance suites

- Depends on: C2–C4. Risk: ADDITIVE. Size: M.
- Deliverables: memory store (write/read/transition CAS/tombstone
  visibility/purge receipt/cross-tenant not-found/grant denial), document store
  (versioned chunk set atomic pointer), vector index (space mismatch rejection,
  delete propagation) fixtures and suites.

- Landed: three reusable suites with fixtures, run by every adapter; shared
  doubles (`MemoryTestData`, request factories) in `AgentKit.Test.Shared`.

### WS14-C7: `AgentKit.Memory.InMemory`

- Depends on: C6. Risk: ADDITIVE new project. Size: M.
- Deliverables: `InMemoryMemoryStore`, `InMemoryDocumentStore`,
  `InMemoryVectorIndex` (brute-force per metric), receipts, keyed registrations;
  all three suites green.

- Landed: `AgentKit.Memory.InMemory` with `AddInMemoryMemoryStore`,
  `AddInMemoryDocumentStore`, `AddInMemoryVectorIndex`; 121 tests including
  conformance, signals, and registration.

### WS14-C8a: `AgentKit.Memory` runtime

- Depends on: C5, C7. Risk: ADDITIVE new project. Size: L.
- Deliverables: `MemoryPolicyProfileKeys`, `AgentMemoryOptions`,
  `MemoryProfileOptions`, registration, `ServiceExtensions`
  (`memory-and-retrieval.md:545-672`), catalogs and selectors, profile runtime
  selector and lease, `FailClosedMemoryPolicy`, policy dispatcher,
  `NoRewriteQueryRewriter`, `BoundedRetrievalBudgetPolicy`, event dispatcher,
  generators, logs and metrics; `memory.*` and `retrieval.*` activity names;
  partial embedding triple fails build.

- Landed: `AgentKit.Memory` options, profile registry and compiled catalog
  (ceilings narrow, never widen; a partial embedding or reranker triple fails
  compilation), runtime selector and lease over keyed collaborators, fail-closed
  policy (`agentkit.fail-closed`) and dispatcher, default acceptance
  `RequireExplicitPolicyAllow`, no-rewrite rewriter, bounded budget policy,
  event dispatcher with required and observational delivery, generators,
  `memory.*` and `retrieval.*` observability, and `ServiceExtensions`. No store,
  index, source, embedding, or reranker is registered by default.

### WS14-C8b: `DefaultMemoryCoordinator`

- Depends on: C8a. Risk: ADDITIVE. Size: M.
- Deliverables: propose/correct/delete with tombstone then purge receipt;
  authorizes through `ISecurityAuthoritySelector`; policy denial before write.

- Landed: `DefaultMemoryCoordinator`: classification ceiling and policy before
  any write, single-use grants per store call, propose, correct with
  replacement, tombstone-then-purge delete, required-sink accounting.

### WS14-C8c: `RetrievalPipeline`

- Depends on: C8a, WS7 executors. Risk: ADDITIVE. Size: L.
- Deliverables: the full pipeline with per-candidate exposure grants and
  provenance; keyword-only path can ship before WS7 embed/rerank.

- Landed: `RetrievalPipeline`: authorize, rewrite, select, embed, search, stale
  filtering against authoritative state, dedupe, rerank with graceful
  degradation, per-candidate exposure grants, budget, required observation; plus
  `DurableMemoryRetrievalSource` and `DocumentChunkRetrievalSource` registered
  explicitly through `AddDurableMemoryRetrievalSource` and
  `AddDocumentRetrievalSource`.

### WS14-C9: `AgentKit.Memory.Sqlite`

- Depends on: C6. Risk: ADDITIVE new project. Size: L.
- Deliverables: memory and document stores; vector index only as a verified
  brute-force scan with `ApproximateSearch=false`; reopen and tombstone
  persistence tests.

- Landed: `AgentKit.Memory.Sqlite` memory and document stores and an exact-scan
  vector index advertising `ApproximateSearch=false`; reopen, tombstone, and
  purge persistence tests.

### WS14-C10: `AgentKit.Memory.Json`

- Depends on: C6. Risk: ADDITIVE new project. Size: M.
- Deliverables: stores over `JsonRecordLog` with tombstone records, torn-tail
  recovery, single-writer lock.

- Landed: `AgentKit.Memory.Json` stores over `JsonRecordLog` with tombstone
  records, torn-tail recovery, and the single-writer lock; the document store
  writes one whole document per line and suits modest documents.

### WS14-C11: Retrieval context contributor

- Depends on: C8c, WS9. Risk: ADDITIVE. Size: M.
- Deliverables: `RetrievalContextContributor` producing `ContextCandidate` with
  `RetrievedData` trust under budget; registration under an assembler key; trust
  never elevates.

- Landed: `AgentKit.Context.Retrieval` (`RetrievalContextContributor`,
  `AddRetrievalContextContributor`): queries with the latest user message under
  the request's captured authorization, publishes `ReferenceData` candidates
  with `ContextTrust.RetrievedData`, preserves identity and provenance, and
  contributes nothing plus a content-free diagnostic when refused. It is a leaf
  because assembler registration needs `AgentKit.Context`.

### WS14-C12: Chunking, source integrity, deletion propagation

- Depends on: C3, C7. Risk: ADDITIVE. Size: M.
- Deliverables: `DeterministicTextChunker`, active-version pointer switch,
  deletion propagation with audit; stale and current never both active.

- Landed: `DeterministicTextChunker` (SHA-256 derived chunk ids over document,
  version, chunker, ordinal, and content) and
  `DefaultDocumentLifecycleCoordinator`: stage inactive, embed, upsert into
  every compatible index, switch the active pointer with the observed expected
  version, then best-effort removal of the superseded version's vectors;
  deletion tombstones, removes the receipt's chunks from every index, and purges
  only when every index is clean, naming uncleaned stores in the receipt.
  Retrieval drops any hit whose version is not active, so stale and current
  chunks are never both returned.

### WS14-C13: Definition key, validator, Simple, documentation

- Depends on: C8a–C12. Risk: DENSE-MODIFY definition, validator, Simple. Size:
  M.
- Deliverables: `MemoryProfileKey? MemoryProfile`; validator per
  `memory-and-retrieval.md:722-749`; `WithMemory`; architecture, a new use case,
  skill.

- Landed: `MemoryCompositionValidator` (wired from `AgentCompositionValidator`,
  reports profile-compilation failures as diagnostics),
  `AgentKit.Simple.WithMemory` with `SimpleMemoryOptions` (fail-closed unless
  `AcceptProposals`), API snapshots for the new packages, architecture document
  recorded deviations, AGENTS.md and skill updates.

## Totals

S 2, M 9, L 4. Confidence high that nothing exists; medium-low on schedule
because C5, C8c, and C11 are hard-blocked on WS7 and WS9.
