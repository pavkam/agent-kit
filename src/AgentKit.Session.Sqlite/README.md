# AgentKit.Session.Sqlite

`AgentKit.Session.Sqlite` provides durable, host-local implementations of
`ISessionStore` and `ISessionDirectory`. Both can share one explicitly selected
SQLite database; the directory is a table in that database, not a second
projection database.

```csharp
var target = new SqliteSessionStoreTarget(
    Path.GetFullPath("data/agentkit-sessions.db"),
    new SqliteSessionStoreInstanceId(configuredInstanceId),
    SqliteDatabaseOpenMode.CreateIfMissing,
    SqliteSchemaMode.ApplyKnownMigrations);

services.AddSqliteSessionStore(target);
services.AddSqliteSessionDirectory(
    new ComponentId("my-app.session-directory"),
    target);
```

Both registrations accept either a prebuilt, validated
`SqliteSessionStoreSettings` or a configure delegate over the mutable
`SqliteSessionStoreOptions`. The delegate runs once at registration; its values
are frozen into an immutable `SqliteSessionStoreSettings` and validated
immediately, so an invalid bound throws `ArgumentOutOfRangeException` before
anything is registered. The options object itself is never added to the
container.

```csharp
services.AddSqliteSessionStore(target, options =>
{
    options.LockTimeout = TimeSpan.FromSeconds(10);      // whole seconds, >= 1s
    options.MaximumEntryPayloadBytes = 4 * 1024 * 1024;  // per committed entry
    options.MaximumIssuedReadSnapshots = 1_024;          // exact-continuation evidence
});
services.AddSqliteSessionDirectory(
    new ComponentId("my-app.session-directory"),
    target,
    options => options.LockTimeout = TimeSpan.FromSeconds(10));
```

`LockTimeout` is the SQLite busy timeout for every connection.
`MaximumEntryPayloadBytes` bounds the encoded payload of every entry the store
commits, caller-appended or store-authored; the store encodes each entry once
during its codec preflight and an oversized entry is rejected with that
operation's typed failure (for example `SessionAppendFailed`) before any process
or database state changes. `MaximumIssuedReadSnapshots` bounds how many
adapter-issued paged-read snapshots one store instance keeps in process; once
exceeded, the oldest is evicted and continuing from it fails with
`SessionReadFailed`.

Store and directory registrations capture their target and settings in their own
factories and publish no ambient `SqliteSessionStoreSettings` singleton. Store
registration is additive, and a repeated `AddSqliteSessionStore` call keeps the
first captured bounds; directory registration is singular with `TryAdd`
semantics, so the first call's bounds win.

The target path must be absolute and its parent directory must already exist.
Registration and construction never create parent directories. Use
`CreateIfMissing` to permit creation of the database file or `OpenExisting` to
require an existing file. `ApplyKnownMigrations` creates and advances schemas
known to this package; the validate-only schema mode requires compatible schema
objects to exist already. The configured store instance ID must match the ID
persisted in an existing database. The store and the directory apply the same
rules: each validates its own table and the persisted instance identity, and
neither installs or migrates schema unless `ApplyKnownMigrations` is selected.

Polymorphic message, content-part, and correlation values inside persisted state
use the shared `$kind` discriminators from
`AgentKit.Session.PortableSessionJsonPolymorphism`; this package does not
declare its own map. Earlier builds wrote `StructuredDataPart` state with a
`json` discriminator that the portable entry codecs never recognized; that value
now reads and writes as `structured` everywhere.

Committed descriptors, entries, branch state, execution lanes, accepted-run
state, idempotency receipts, directory ownership, and routing survive disposal
and process restart. Reopening uses the same absolute target and store instance
ID. Portable, versioned session-entry codecs reconstruct framework entries;
unknown or invalid durable payloads are rejected instead of activated through
CLR type names. Appending an entry kind with no registered codec returns the
operation's typed failure before any session state changes.

## Storage schema

Every session, branch, entry, lane, admission, and idempotency receipt is its
own row — not a field inside one whole-store JSON blob:

| Table                                | One row per                                                                                       | Key                                                                                  |
| ------------------------------------ | ------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| `agentkit_sessions`                  | session                                                                                           | `(agent_id, session_id)`                                                             |
| `agentkit_session_branches`          | branch                                                                                            | `(agent_id, session_id, branch_id)`                                                  |
| `agentkit_session_entries`           | committed entry                                                                                   | `(agent_id, session_id, branch_id, sequence)`                                        |
| `agentkit_session_lanes`             | execution lane                                                                                    | `(agent_id, session_id, lane_id)`                                                    |
| `agentkit_session_admissions`        | admitted input                                                                                    | `(agent_id, session_id, admission_id)`, unique on `(agent_id, session_id, input_id)` |
| `agentkit_session_scope_idempotency` | session/branch-scoped receipt (branch, lane-provision, admission, run-start, run-release, append) | `(agent_id, session_id, scope_kind, branch_id, idempotency_key)`                     |
| `agentkit_store_scope_idempotency`   | tenant/agent-scoped receipt (create, deleted-create, delete)                                      | `(scope_kind, tenant_id, agent_id, session_id, idempotency_key)`                     |

`agentkit_session_directory_locations`,
`agentkit_session_directory_creation_routes`, and
`agentkit_session_directory_write_routes` provide the same shape for
`ISessionDirectory`; a routed address's location and its recording owner share
one row instead of two dictionaries that could disagree.

Ordinary appends, admissions, lane provisioning, and run acceptance validate
sequence contiguity and entry/message identity reservation through indexed SQL
lookups and a cached per-branch tip; they never decode a previously committed
entry's payload. Branch forking copies a parent branch's committed rows up to
the fork point without decoding them either. Only a paged read decodes entries,
and only the exact bounded page it requested, so a corrupt or unreadable entry
can affect only a read that actually names its row — never a scan across the
whole store. Every entry an operation intends to commit is still encoded once,
through the same captured codec catalog, before any row is written
(`SqliteSessionStore.CanPersist`), so an unencodable or oversized entry still
surfaces as that operation's typed failure instead of a mid-write exception.

`SessionVersion` remains the one whole-session optimistic-concurrency token. A
mutation reads a session's row and every row it needs inside one non-deferred
(`BEGIN IMMEDIATE`) SQLite transaction — which acquires the database's write
lock before that read happens — so the compare against `ExpectedVersion` and the
transaction's commit are atomic without any process-local semaphore. Reads run
inside a deferred transaction so a multi-table load (for example a branch's tip
plus its idempotency receipt) observes one consistent snapshot.

**Breaking on-disk format change.** This schema replaces an earlier
single-row-blob shape (`agentkit_session_metadata`/`agentkit_session_directory`
tables whose sole `state_json` column held the JSON serialization of the entire
process state). That format was never released, so
`SqliteSchemaMode.ApplyKnownMigrations` never reads or reinterprets it: it
creates the tables above fresh, leaving any pre-existing blob-shaped table
untouched and orphaned in the same file. `SqliteSchemaMode.ValidateExact`
against a database that only has the old blob tables fails with a typed
`InvalidOperationException` instead of silently reinterpreting it.

SQLite transactions provide strong local consistency and serialize competing
writers across processes using the same file. This package does not claim
distributed leases, distributed fencing, cross-database atomicity, or network
filesystem correctness. Callers must quiesce active store operations before
disposing a directly constructed store.

The package registers no grant store or audit dispatcher. Applications must
compose those security authorities explicitly before resolving the store or
directory.
