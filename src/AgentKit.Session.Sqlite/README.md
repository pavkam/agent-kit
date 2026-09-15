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
operation's typed failure (for example `SessionAppendFailed`) before any
process or database state changes. `MaximumIssuedReadSnapshots` bounds how many
adapter-issued paged-read snapshots one store instance keeps in process; once
exceeded, the oldest is evicted and continuing from it fails with
`SessionReadFailed`.

Store registrations are additive and capture their target and settings in the
store factory; they publish no ambient `SqliteSessionStoreSettings` singleton,
and a repeated `AddSqliteSessionStore` call keeps the first captured bounds.
The directory registration is singular and does register its effective
`SqliteSessionStoreSettings` with `TryAdd` semantics so composition can observe
the directory's bounds; that singleton does not feed the store.

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
