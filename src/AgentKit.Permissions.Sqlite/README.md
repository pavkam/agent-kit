# AgentKit.Permissions.Sqlite

Durable local SQLite storage for AgentKit security grants, use counts,
revocation, and exact enforcement-intent receipts. Hosts explicitly supply one
fixed trusted bootstrap target; the adapter never creates parent directories or
selects a fallback store.

`AddSqliteSecurityGrantStore` registers one additive `ISecurityGrantStore`
selection without opening the database. Trusted host bootstrap resolves the
selected interface, verifies `SqliteSecurityGrantStore`, and calls
`InitializeAsync`. Repeating the same leaf is idempotent; another leaf or custom
store remains visible so composition can reject ambiguity. Hosts deliberately
replace a selection by removing all `ISecurityGrantStore` registrations first.
Pass explicit `SqliteSecurityGrantStoreSettings`, or adjust the defaults with
an optional configure delegate, for example
`services.AddSqliteSecurityGrantStore(target, o => o.MaximumClaims = 64);`.
Invalid bounds throw `ArgumentOutOfRangeException` at registration.

Initialization may create only the exact configured file in an existing,
non-linked directory hierarchy. It transactionally adopts a structurally empty
version-zero file as recovery from interrupted creation, pins the configured
store identity, validates the exact version-one schema, and establishes WAL.
Normal operations revalidate target, schema, and identity but inspect only the
accessed bounded rows; full database integrity checking belongs to explicit
bootstrap or maintenance. The Microsoft SQLite provider executes these APIs
synchronously, so cancellation is observed at defined boundaries around the
bounded provider calls. SQLite coordinates a shared file on one host and does
not provide distributed fencing.

A process or storage failure can leave the caller uncertain whether SQLite
committed a consumption acknowledgement. Recovery retries the exact same grant,
enforcement request, and enforcement-intent identity; a `Reconciled` result is
historical receipt evidence and never fresh authority to repeat the protected
effect. The legacy consumption operation without an enforcement intent cannot be
safely retried automatically after an uncertain persistence acknowledgement. The
adapter retains grants and receipts indefinitely; host maintenance owns any
future bounded archival policy. Target-path and store-identity checks detect
accidental replacement but are not an operating-system isolation boundary.
