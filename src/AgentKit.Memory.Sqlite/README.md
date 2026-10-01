# AgentKit.Memory.Sqlite

Durable, host-local SQLite storage for AgentKit durable memory, documents, and
vectors.

`AddSqliteMemoryStore`, `AddSqliteDocumentStore`, and `AddSqliteVectorIndex`
register keyed `IMemoryStore`, `IDocumentStore`, and `IVectorIndex`
implementations over an explicit, host-authorized database file
(`SqliteMemoryTarget`). Each store needs its own database. No persistence target
is ever chosen implicitly.

## Behavior

- Every mutation runs in an immediate transaction that reads stored state
  through the same shared planner the other adapters run, so the version check,
  idempotent replay, sequence allocation, and deletion generation are atomic
  even across processes sharing the file. An acknowledged write is committed and
  survives process loss.
- Tombstones and deletion generations persist through reopening, so a restored
  database cannot resurrect a deleted record for retrieval.
- A document publication replaces one row that contains every version, chunk
  set, and the active-version pointer, so a publication and its pointer switch
  commit atomically and a failed write leaves the prior version active.
- The vector index is **not** an approximate or extension-backed index. SQLite
  stores the vector bytes and a bounded idempotency-receipt window, and search
  is an exact scan scoring every visible vector under the space's metric
  (`ApproximateSearch` is `false`), proven by the shared vector conformance
  suite. A request whose complete vector-space descriptor differs from the
  index's is refused before any grant is consumed or row is read.
- SQLite provides durable local storage only. It does not imply distributed
  indexing, remote replication, distributed leases, or atomic transactions with
  artifact or session storage.

All three stores run the shared conformance suites in
`tests/AgentKit.Conformance`.
