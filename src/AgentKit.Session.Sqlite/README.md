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

The target path must be absolute and its parent directory must already exist.
Registration and construction never create parent directories. Use
`CreateIfMissing` to permit creation of the database file or `OpenExisting` to
require an existing file. `ApplyKnownMigrations` creates and advances schemas
known to this package; the validate-only schema mode requires compatible schema
objects to exist already. The configured store instance ID must match the ID
persisted in an existing database. The store and the directory apply the same
rules: each validates its own table and the persisted instance identity, and
neither installs or migrates schema unless `ApplyKnownMigrations` is selected.

Polymorphic message, content-part, and correlation values inside persisted
state use the shared `$kind` discriminators from
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

SQLite transactions provide strong local consistency and serialize competing
writers across processes using the same file. This package does not claim
distributed leases, distributed fencing, cross-database atomicity, or network
filesystem correctness. Callers must quiesce active store operations before
disposing a directly constructed store.

The package registers no grant store or audit dispatcher. Applications must
compose those security authorities explicitly before resolving the store or
directory.
