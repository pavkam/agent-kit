# WS14: Memory and retrieval

Goal: durable memory, source documents, vector indexes, policy-governed proposal
and correction, a retrieval pipeline (authorize → rewrite → select → search →
rerank → dedupe → expose-authorize → budget), provenance-carrying context
contribution, chunking with source integrity, and deletion with tombstones and
purge receipts — across InMemory, Sqlite, and Json adapters.

Nothing in this workstream exists today except `MemoryProfileKey`
(`Abstractions/Identity/MemoryProfileKey.cs:8`, un-wired) and
`EmbeddingSpaceIdentity` (used on the embedding result path). No
`AgentKit.Memory*` project exists; the architecture tests already whitelist
`AgentKit.Memory` and forbid `Memory → Session`.

Owning documents:
[Memory and retrieval](../architecture/memory-and-retrieval.md),
[Memory, retrieval, storage](../concepts/memory-retrieval-and-storage.md).

## Progress

- [ ] WS14-C1 classification, provenance, identities
- [ ] WS14-C2 `MemoryOperationContext`, records, `IMemoryStore` family
- [ ] WS14-C3 document contracts
- [ ] WS14-C4 vector contracts
- [ ] WS14-C5 policy, coordinator, retrieval, profile runtime, events
- [ ] WS14-C6 conformance suites
- [ ] WS14-C7 `AgentKit.Memory.InMemory`
- [ ] WS14-C8a `AgentKit.Memory` runtime
- [ ] WS14-C8b `DefaultMemoryCoordinator`
- [ ] WS14-C8c `RetrievalPipeline`
- [ ] WS14-C9 `AgentKit.Memory.Sqlite`
- [ ] WS14-C10 `AgentKit.Memory.Json`
- [ ] WS14-C11 retrieval context contributor
- [ ] WS14-C12 chunking, source integrity, deletion propagation
- [ ] WS14-C13 definition key, validator, Simple, documentation

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

### WS14-C2: `MemoryOperationContext`, records, `IMemoryStore` family

- Depends on: C1. Risk: ADDITIVE. Size: M.
- Deliverables: `MemoryOperationContext` (validates identity/authorization
  agreement), `MemoryContent`, `MemoryProposal`, `DurableMemoryRecord`,
  `MemoryStoreDescriptor`, catalog and selector, selection request/result,
  `IMemoryStore`, write/read/transition/delete requests and results,
  `MemoryDeletionReceipt` separating logical and physical deletion; all carrying
  `SecurityGrant`.

### WS14-C3: Document contracts

- Depends on: C1. Risk: ADDITIVE. Size: S.
- Deliverables: `DocumentRecord`, `DocumentMetadata`, `DocumentStoreDescriptor`,
  `IDocumentStore`, write/read/delete requests and results, `DocumentChunk`,
  `ChunkerVersion`, `IDocumentChunker`; bytes via `ArtifactReference`.

### WS14-C4: Vector contracts

- Depends on: C1, WS7-C7. Risk: ADDITIVE. Size: S.
- Deliverables: `VectorSpaceDescriptor`, `IVectorIndex`, upsert/search/delete
  requests and results, `VectorRecord`; incompatible space descriptor rejected
  before search.

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

### WS14-C6: Conformance suites

- Depends on: C2–C4. Risk: ADDITIVE. Size: M.
- Deliverables: memory store (write/read/transition CAS/tombstone
  visibility/purge receipt/cross-tenant not-found/grant denial), document store
  (versioned chunk set atomic pointer), vector index (space mismatch rejection,
  delete propagation) fixtures and suites.

### WS14-C7: `AgentKit.Memory.InMemory`

- Depends on: C6. Risk: ADDITIVE new project. Size: M.
- Deliverables: `InMemoryMemoryStore`, `InMemoryDocumentStore`,
  `InMemoryVectorIndex` (brute-force per metric), receipts, keyed registrations;
  all three suites green.

### WS14-C8a: `AgentKit.Memory` runtime

- Depends on: C5, C7. Risk: ADDITIVE new project. Size: L.
- Deliverables: `MemoryPolicyProfileKeys`, `AgentMemoryOptions`,
  `MemoryProfileOptions`, registration, `ServiceExtensions`
  (`memory-and-retrieval.md:545-672`), catalogs and selectors, profile runtime
  selector and lease, `FailClosedMemoryPolicy`, policy dispatcher,
  `NoRewriteQueryRewriter`, `BoundedRetrievalBudgetPolicy`, event dispatcher,
  generators, logs and metrics; `memory.*` and `retrieval.*` activity names;
  partial embedding triple fails build.

### WS14-C8b: `DefaultMemoryCoordinator`

- Depends on: C8a. Risk: ADDITIVE. Size: M.
- Deliverables: propose/correct/delete with tombstone then purge receipt;
  authorizes through `ISecurityAuthoritySelector`; policy denial before write.

### WS14-C8c: `RetrievalPipeline`

- Depends on: C8a, WS7 executors. Risk: ADDITIVE. Size: L.
- Deliverables: the full pipeline with per-candidate exposure grants and
  provenance; keyword-only path can ship before WS7 embed/rerank.

### WS14-C9: `AgentKit.Memory.Sqlite`

- Depends on: C6. Risk: ADDITIVE new project. Size: L.
- Deliverables: memory and document stores; vector index only as a verified
  brute-force scan with `ApproximateSearch=false`; reopen and tombstone
  persistence tests.

### WS14-C10: `AgentKit.Memory.Json`

- Depends on: C6. Risk: ADDITIVE new project. Size: M.
- Deliverables: stores over `JsonRecordLog` with tombstone records, torn-tail
  recovery, single-writer lock.

### WS14-C11: Retrieval context contributor

- Depends on: C8c, WS9. Risk: ADDITIVE. Size: M.
- Deliverables: `RetrievalContextContributor` producing `ContextCandidate` with
  `RetrievedData` trust under budget; registration under an assembler key; trust
  never elevates.

### WS14-C12: Chunking, source integrity, deletion propagation

- Depends on: C3, C7. Risk: ADDITIVE. Size: M.
- Deliverables: `DeterministicTextChunker`, active-version pointer switch,
  deletion propagation with audit; stale and current never both active.

### WS14-C13: Definition key, validator, Simple, documentation

- Depends on: C8a–C12. Risk: DENSE-MODIFY definition, validator, Simple. Size:
  M.
- Deliverables: `MemoryProfileKey? MemoryProfile`; validator per
  `memory-and-retrieval.md:722-749`; `WithMemory`; architecture, a new use case,
  skill.

## Totals

S 2, M 9, L 4. Confidence high that nothing exists; medium-low on schedule
because C5, C8c, and C11 are hard-blocked on WS7 and WS9.
