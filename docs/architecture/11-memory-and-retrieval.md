# Memory and retrieval

**Role:** Retain and retrieve durable knowledge without confusing it with
conversation history or model context.

This component separates durable memory, source documents, embeddings, vector
indexes, and retrieval. Session history remains with sessions, working context
remains with context assembly, and embedding generation remains a provider
operation.

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

Retrieval is not context assembly. It proposes authorized candidates; the
context component decides which candidates fit and records the final manifest.
Retrieved content remains untrusted data and cannot become system instruction.

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
