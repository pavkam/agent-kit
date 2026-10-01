# AgentKit.Memory.InMemory

Explicitly ephemeral, process-local memory, document, and vector storage for
AgentKit.

`AddInMemoryMemoryStore(MemoryStoreKey)`,
`AddInMemoryDocumentStore(DocumentStoreKey)`, and
`AddInMemoryVectorIndex(VectorSpaceDescriptor)` register keyed `IMemoryStore`,
`IDocumentStore`, and `IVectorIndex` implementations. Nothing here survives the
process, so selecting these is the application's explicit act of accepting
ephemeral state; no other package ever registers them implicitly.

## Behavior

- State is partitioned by tenant. The same identity in two tenants is two items,
  and one tenant never observes another's. Another agent or another principal's
  private item in the same tenant is reported as not found.
- Every operation consumes a single-use grant that binds that exact operation
  before any state is read or written.
- Memory creation, transitions, and deletion are idempotent and version-checked.
  Deletion commits a tombstone before an optional purge and assigns a store-wide
  deletion generation.
- A document publication stores a complete versioned chunk set and can switch
  the active-version pointer in the same committed step.
- The vector index scores every visible vector with the space's metric
  (`ApproximateSearch` is `false`) and refuses any request whose complete space
  descriptor differs from its own before it consumes a grant or reads state.

All three stores run the shared conformance suites in
`tests/AgentKit.Conformance`.
