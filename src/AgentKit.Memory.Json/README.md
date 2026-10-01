# AgentKit.Memory.Json

Durable, inspectable, host-local JSONL storage for AgentKit durable memory,
documents, and vectors.

`AddJsonMemoryStore`, `AddJsonDocumentStore`, and `AddJsonVectorIndex` register
keyed `IMemoryStore`, `IDocumentStore`, and `IVectorIndex` implementations over
an explicit, host-authorized root directory (`JsonMemoryTarget`). Each store
needs its own root: the advisory exclusive lock admits one writer per root, so
the adapter claims no multi-process coordination. No persistence target is ever
chosen implicitly.

## Behavior

- Every acknowledged mutation appends one flushed newline-delimited record, so
  an acknowledged record, transition, publication, batch, or tombstone survives
  process loss. A torn trailing append is recovered or refused according to the
  target's `JsonStoreRecoveryMode`.
- Live state is rebuilt by replaying the log through the same shared planner
  state the in-memory adapter runs, so all adapters agree on scoping,
  idempotency, versioning, and deletion. Tombstones and deletion generations
  survive replay and compaction, so a restored log cannot resurrect a deleted
  record for retrieval.
- A document line carries the whole document including its active-version
  pointer, so a publication is atomic across replay. The adapter suits modest
  local documents.
- The vector index is an exact brute-force scan (`ApproximateSearch` is
  `false`); it refuses any request whose complete vector-space descriptor
  differs from its own before it consumes a grant.
- Every operation consumes a single-use grant that binds that exact operation.

All three stores run the shared conformance suites in
`tests/AgentKit.Conformance`.
