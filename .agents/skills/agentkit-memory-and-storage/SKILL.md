---
name: agentkit-memory-and-storage
description:
  "Design or debug AgentKit durable memory, document stores, vector indexes,
  retrieval, provenance, retention, and deletion. Use for AgentKit.Memory and
  backend leaves; not sessions, context, artifacts, or provider embedding work."
---

# AgentKit Memory and Retrieval

Follow
[Memory and retrieval](../../../docs/architecture/memory-and-retrieval.md) and
the
[normative memory boundaries](../../../docs/concepts/memory-retrieval-and-storage.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

## Decision guide

1. Keep the categories separate: durable memory, source documents and chunks,
   vector indexes, retrieval policy, and semantic-operation results are not one
   universal `IMemory` service.
2. `AgentKit.Memory` owns memory lifecycle and retrieval coordination. Document,
   memory, and vector backends are independently selectable leaf integrations;
   their neutral contracts live in `AgentKit.Abstractions`.
3. Session history remains with `AgentKit.Session`; model-ready working context
   remains with `AgentKit.Context`; durable bytes remain with
   `AgentKit.Artifacts`; embedding and reranking generation remain provider
   operations. Route changes at those boundaries to their owning skills.
4. Define tenant and principal visibility, agent and source scope, typed
   identities, lifecycle state, versions, concurrency, consistency, pagination,
   retention, correction, and deletion before choosing a backend.
5. Make proposed memory distinct from accepted durable memory. Preserve source
   identity, provenance, classification, retention, and policy decisions.
6. Bind each vector to an explicit embedding-space identity, dimensions,
   modality, normalization, source version, and chunker version. Reject
   incompatible queries before index access.
7. Treat retrieved content and metadata as untrusted. Authorize retrieval and
   model exposure separately; apply explicit budgets, filters, ranking, and
   provenance.
8. Keep retries and idempotency with the operation that can judge safety.
   Cancellation and partial failure must not fabricate committed memory.
9. Test isolation, optimistic concurrency, duplicate writes, pagination, expiry,
   correction and deletion, vector incompatibility, deterministic ranking,
   provenance, redaction, and migrations.

## Landed shape

- Packages: contracts in `AgentKit.Abstractions`; runtime in `AgentKit.Memory`;
  store leaves `AgentKit.Memory.{InMemory,Sqlite,Json}` over shared source-only
  planner folders `AgentKit.Memory.Storage.{Shared,Durable}`; context
  contribution in the leaf `AgentKit.Context.Retrieval`. The facade validates
  profiles (`MemoryCompositionValidator`) and never references
  `AgentKit.Memory`.
- Coordinators (`IMemoryCoordinator`, `IDocumentLifecycleCoordinator`) take
  context-carrying commands and ask the captured authority for one single-use
  grant per store call; stores take the exact grant.
- Retention is fail-closed (`RequireExplicitPolicyAllow`,
  `agentkit.fail-closed`). No store, index, source, embedding model, or reranker
  is installed by default.
- Memory hooks (`BeforeMemoryProposal`, `BeforeMemoryWrite`, `BeforeRetrieval`,
  `BeforeRetrievalExposure`) are reached through the `HookDispatchContext?` the
  coordinator and pipeline accept; `MemoryHookRunner` derives one dispatch per
  point and refuses fail-closed on a hook fault. Hooks only veto, lower the
  budget, or drop candidates; delete dispatches none. In-run retrieval gets the
  context through `ContextAssemblyRequest.Hooks` and
  `ContextContributionRequest.Hooks`, which the assembler only forwards.
- Retrieval drops stale, unauthorized, duplicate, and over-budget candidates and
  fails closed when exposure authorization or required observation is
  unavailable; candidates stay `UntrustedData` with their provenance.
- Publication is stage, embed, index, activate; deletion is tombstone, vector
  cleanup, purge, with uncleaned stores named in the receipt.
- Adapter capabilities are honest: SQLite vectors are an exact scan
  (`ApproximateSearch=false`); Json holds an exclusive lock and claims no
  multi-process coordination. Run all three conformance suites for any new
  adapter.

Publish complete indexed versions through an atomic active-version pointer.
Tombstones exclude data from new retrieval/exposure before physical cleanup;
revalidate stale deletion/revocation generations before egress and distinguish
logical invisibility from completed purge.

Do not leak provider SDK, database client, or vector-query types into neutral
contracts, and do not turn this skill into guidance for every persistent state
in AgentKit.
