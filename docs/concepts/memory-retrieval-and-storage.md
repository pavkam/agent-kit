# Memory, retrieval, and storage

**Status:** Normative boundaries

**Architecture:**
[Memory and retrieval](../architecture/memory-and-retrieval.md)

**Depends on:** [Architecture](architecture-and-dependency-boundaries.md),
[permissions](permissions-approvals-and-trust.md)

## Purpose

This boundary keeps [session history](sessions-persistence-and-branching.md),
[working context](context-assembly-and-instructions.md), durable memory,
documents, vectors, and retrieval from becoming one policy-free store.

“Memory” is not one interface. AgentKit distinguishes conversation history,
working context, durable memory, source documents, embeddings, vector indexes,
and retrieval so policy and storage can vary independently.

## State categories

| Category             | Meaning                                         | Canonical owner       |
| -------------------- | ----------------------------------------------- | --------------------- |
| Conversation history | Ordered record of a session branch              | Session store         |
| Working context      | Bounded input for one model request             | Context assembler     |
| Durable memory       | Selected facts/preferences retained across runs | Memory store + policy |
| Document/object      | Source material and metadata                    | Document store        |
| Embedding            | Vector derived from source/model                | Embedding provider    |
| Vector index         | Search over one compatible vector space         | Vector index          |
| Retrieval            | Authorized selection/ranking of candidates      | Retrieval pipeline    |

These MUST NOT be collapsed into one `IMemory` service.

## Scope and identity

Every persisted item MUST carry tenant, owner/principal visibility, namespace,
source identity/version, classification, timestamps, retention, and version.
Keys SHOULD make cross-tenant or cross-user reads structurally difficult.

Every memory or retrieval operation MUST also carry the complete authenticated
`ExecutionIdentity`, typed agent/session/operation correlation, immutable
authorization snapshot, and exact memory-profile key and version. Durable
tenant/owner fields are routing and visibility projections; they do not replace
the identity that authorized the operation.

Storage contracts define consistency, atomicity, optimistic concurrency,
pagination, duplicate/idempotent writes, deletion, expiry, and failure behavior.
Provider SDK and database query types stay in integration packages.

## Durable memory lifecycle

Memory writing is a protected policy decision with explicit states:

```text
Proposed -> Validated -> Accepted -> Active
                          |            |
                          v            v
                       Rejected     Corrected | Deleted | Expired
```

A model may propose memory but cannot self-authorize retention. The shared
security authority evaluates reads, writes, deletion, retrieval exposure, and
embedding egress after canonicalization. Validation checks provenance, scope,
sensitivity, contradiction, and policy. Corrections append versions or
tombstones; they do not rewrite unrelated history.

Memory records distinguish verified fact, user preference, instruction,
decision, summary, and uncertain claim. Unverified guesses MUST not be stored as
facts.

## Embeddings

Embedding generation follows the
[independent semantic-operation contract](../providers/semantic-operations.md);
the memory subsystem owns space compatibility and vector lifecycle, not model
invocation.

Each vector MUST store embedding provider, model, revision, dimensions,
modality, normalization, distance metric compatibility, source hash, chunk ID,
and creation time. The vector index MUST reject an incompatible query before
search.

Re-embedding is a versioned migration that builds a compatible index and swaps
an alias/pointer atomically. Mixing vector spaces because dimensions happen to
match is forbidden.

## Retrieval pipeline

Retrieval MUST separate:

1. authorize query and candidate scope through the security authority;
2. optionally rewrite query;
3. select sources/indexes;
4. search with stable filters;
5. rerank and deduplicate;
6. authorize each result and provider destination for model exposure;
7. enforce item/token/byte budgets; and
8. produce typed context candidates with provenance and trust class.

At run-plan compilation, the selected memory profile is frozen as an immutable
versioned snapshot. At invocation, one runtime selector activates an owned lease
containing the exact keyed sources/stores, provider operations, security
authority selector, optional versioned query rewriter, budgets, and event
dispatcher for that snapshot. Pipelines MUST use that lease rather than inject
global selectors independently or resolve keyed services from
`IServiceProvider`; this prevents collaborators from two agents or profile
versions being mixed in one operation. A profile with rewriting disabled has no
rewriter capability; an enabled profile receives exactly its selected key and
version.

Embedding and reranking require an explicit selector key, executor key, and
ordered alias policy in that snapshot. A partial selection is invalid; the
runtime never substitutes an engine-global semantic-operation default.

Retrieved content is untrusted data, never host instruction. The context
assembler delimits it and records which items were included or omitted.

## Chunking and source integrity

Chunk identity includes source version/hash and deterministic chunker version.
Updates must not leave stale chunks active beside replacements. Deletion and
authorization revocation propagate to documents, chunks, vectors, caches, and
retrieval results under documented consistency.

The original authorized source or immutable reference SHOULD remain available
for citation. Generated summaries must not impersonate source text.

## Privacy and deletion

Data classification controls encryption, region, retention, logging, and model
exposure. Stores MUST support scoped deletion and tombstones where eventual
indexes/caches need propagation. Deletion jobs are auditable and must not leak
the deleted content into diagnostics.

Source publication and deletion follow the
[consistency contract](../architecture/memory-and-retrieval.md#publication-deletion-and-exposure-consistency).
An atomic active-version pointer exposes a complete indexed chunk set. A logical
deletion tombstone immediately excludes that source version from new retrieval
and exposure, even when physical index/cache cleanup is pending. Exposure binds
a deletion/revocation generation and MUST revalidate a stale generation before
egress. Receipts distinguish logical invisibility, pending purge, and completed
physical deletion; backup replay cannot resurrect a logically deleted version.

## Acceptance scenarios

- Cross-tenant lookup fails without revealing item existence.
- Incompatible embedding revision is rejected before vector query.
- Retrieved prompt injection remains data in the provider request.
- Memory proposal requires policy acceptance before later retrieval.
- Source update cannot return stale and current chunks as one version.
- Deletion propagates through index/cache and remains auditable without content.

## Related specifications

- [History validation and repair](history-validation-and-repair.md)
- [Model providers and capabilities](model-providers-and-capabilities.md)
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md)
