# AgentKit.Session.Json

Durable host-local JSON storage for AgentKit sessions. The package provides two
independent leaves:

- `JsonSessionStore` implements `ISessionStore` under the store key
  `agentkit.json`.
- `JsonSessionDirectory` implements `ISessionDirectory`.

Both are log structured. Every accepted mutation is appended as exactly one
newline-delimited JSON record and flushed to disk before the in-memory
projection changes, so an acknowledged effect survives process loss. Live state
is rebuilt by replaying the log during `InitializeAsync`.

## Durability model

- One advisory exclusive lock is held per store root for the adapter's lifetime,
  so a second writer on the same host fails fast instead of interleaving
  appends.
- The manifest binds the root to a persistent store identity, a schema version,
  and the fingerprint of the effective JSON encoding contract.
- A record log that ends in an incomplete append is recoverable only under
  `JsonStoreRecoveryMode.RecoverTornAppends`.
- The store and the directory each own a complete root, including its own
  manifest and advisory lock. They must not be pointed at the same directory.

This is durable single-process host-local storage. It provides no distributed
lease, no fencing token, and no atomicity with an external effect, so
`SessionStoreDescriptor.SupportsDistributedFencing` is `false`.

## Record model

The log is a command log. Each record carries the exact immutable request that
was accepted plus the values the adapter itself generated for that commit (a new
`BranchId`, a clock-derived commit instant). Replay re-executes the same
deterministic commit path against the projection, so optimistic-concurrency
versions, per-lane cursors and revisions, installed accepted run state, and
every idempotency receipt are reproduced exactly as they were before restart.

Session entries inside those requests are encoded through the shared
`ISessionEntryCodecCatalog` wire envelopes from `AgentKit.Session`, so an entry
written here reads back through any other first-party durable adapter.

## Registration

```csharp
services.AddJsonSessionStore(new JsonSessionStoreTarget(
    "/var/lib/app/sessions",
    new JsonSessionStoreInstanceId(storeId),
    JsonStoreOpenMode.CreateIfMissing,
    JsonStoreRecoveryMode.RecoverTornAppends));

services.AddJsonSessionDirectory(
    new ComponentId("app.session.directory"),
    new JsonSessionDirectoryTarget(
        "/var/lib/app/session-directory",
        new JsonSessionDirectoryInstanceId(directoryId),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends));
```

Trusted bootstrap resolves the registered contract, verifies the concrete type,
and calls `InitializeAsync` once before first use.
