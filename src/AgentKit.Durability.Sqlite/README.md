# AgentKit.Durability.Sqlite

Durable host-local SQLite storage for AgentKit's durable-execution contracts:
`IDurableOperationJournal`, `IDurableLeaseManager`, and a matching
`IDurableExecutionBackend`.

## What it guarantees

- **Durability across process restarts on one host.** Every acknowledged write
  is committed in an immediate SQLite transaction with `synchronous = FULL`
  under write-ahead journaling.
- **Real fencing.** Ownership generations are allocated by the database with
  `UPDATE durable_lease_sequence SET next_token = next_token + 1 … RETURNING next_token`,
  inside the same transaction that installs the lease row. Two processes racing
  for the same operation receive strictly increasing tokens and exactly one
  installs a row.
- **Protected, audited journal access.** Each write consumes its single-use
  `SecurityGrant` and completes required audit before anything is persisted. An
  unavailable grant store or audit dispatcher denies the write rather than
  recording unaudited state.

## What it does not claim

SQLite is durable local storage. It is **not** a distributed lease service. This
package claims host-local multi-process exclusion only: processes sharing the
exact same database file exclude each other, and nothing else does. Two hosts,
two database files, or a networked filesystem produce no mutual exclusion. The
backend descriptor therefore reports `SupportsDistributedOwnership: false`,
`SupportsExternalHandoff: false`, and `SupportsReconciliation: false`, so an
unknown effect escalates to an operator instead of being optimistically retried.

## Composition

The journal and the lease manager must share one `SqliteDurableDatabase`
instance — a token only fences a write when both are allocated and enforced
inside one atomic store.

```csharp
var database = new SqliteDurableDatabase(
    new SqliteDurableStoreTarget(
        databasePath: "/var/lib/myapp/durability.db",
        expectedStoreInstanceId: new SqliteDurableStoreInstanceId(storeId),
        openMode: SqliteDatabaseOpenMode.CreateIfMissing,
        schemaMode: SqliteSchemaMode.ApplyKnownMigrations),
    SqliteDurableStoreSettings.CreateDefault());

services
    .AddSqliteDurableExecutionBackend(new DurableBackendKey("local"))
    .AddSqliteDurableLeaseManager(new DurableLeaseManagerKey("local"), database)
    .AddSqliteDurableOperationJournal(new DurableJournalKey("local"), database);
```

Every registration is keyed, because a durability profile selects its journal
and lease manager by exact key and registration order must never choose a
persistence target. The database path, store identity, and bootstrap effects are
external facts the host supplies; this package fabricates none of them.

Call `InitializeAsync` on the journal or the lease manager once during trusted
host bootstrap before any operation is journaled or any lease is acquired.
Initializing the shared database more than once is a no-op.
