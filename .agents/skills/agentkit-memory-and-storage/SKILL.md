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

Publish complete indexed versions through an atomic active-version pointer.
Tombstones exclude data from new retrieval/exposure before physical cleanup;
revalidate stale deletion/revocation generations before egress and distinguish
logical invisibility from completed purge.

Do not leak provider SDK, database client, or vector-query types into neutral
contracts, and do not turn this skill into guidance for every persistent state
in AgentKit.
