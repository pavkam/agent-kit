---
name: agentkit-memory-and-storage
description:
  "Design, implement, or debug AgentKit conversation state, durable memory,
  storage, embeddings, vector indexes, retrieval, and context assembly. Use for
  persistence and RAG boundaries; not for provider chat translation."
---

# AgentKit Memory and Storage

Read [AGENTS.md](../../../AGENTS.md). Name the state category before choosing a
contract:

- conversation history is the ordered record of a run or thread;
- working context is the bounded input assembled for one model request;
- durable memory is selected information retained across runs;
- object/document storage persists source material and metadata;
- an embedding provider creates vectors;
- a vector index searches one compatible vector space; and
- retrieval selects and ranks context candidates.

Do not merge these into one `IMemory` interface.

`AgentKit.IO` coordinates queued input, but durable admission and promotion are
session facts. A session store must keep those facts consistent with the history
version it exposes.

1. Define ownership and isolation: tenant, user, agent, thread, run, namespace,
   and data classification. Keys must make accidental cross-scope reads hard.
2. Define identity, ordering, version/concurrency token, atomicity, pagination,
   retention/TTL, deletion, and consistency for every storage contract.
3. Keep serialization versioned and provider-neutral. Preserve unknown fields
   where forward-compatible round trips matter; make migrations explicit.
4. Store embedding provider/model/revision, dimensions, modality, normalization,
   source hash, and chunk identity with each vector. Reject incompatible queries
   before contacting an index.
5. Separate chunking, embedding, indexing, query rewriting, retrieval,
   reranking, filtering, and context assembly so each can be replaced and
   measured.
6. Treat retrieved text and metadata as untrusted data, never instructions.
   Enforce authorization before retrieval and again before exposing results to a
   model. Preserve provenance and apply explicit context/token budgets.
7. Make durable-memory writes an observable policy decision. Distinguish
   proposed memory from accepted memory and support correction/deletion without
   rewriting unrelated history.
8. Use async APIs for real I/O, cancellation throughout, bounded batch sizes,
   and streaming/pagination only where it avoids materialization. State retry
   and idempotency ownership.
9. Test isolation, optimistic concurrency, ordering, duplicate writes,
   cancellation, partial failures, pagination, expiry, deletion, incompatible
   vector spaces, deterministic ranking, provenance, redaction, and migrations.

Provider SDK types, database clients, and vector-store query objects stay in
leaf integration packages.
