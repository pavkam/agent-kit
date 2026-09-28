# AgentKit.Durability.Json

Durable, human-inspectable JSON storage for AgentKit's
`IDurableOperationJournal`. Operation transitions are appended to a
newline-delimited JSON log under one fixed local root, alongside a manifest that
binds the root's store identity, schema version, and encoding fingerprint.

## What it guarantees

- **Every acknowledged record is flushed before the call returns.** The log
  retains transitions rather than rewritten state, so a record that was
  acknowledged survives process loss.
- **A torn trailing append is recovered.** When the target selects
  `JsonStoreRecoveryMode.RecoverTornAppends`, initialization discards an
  incomplete final record and compacts the log; under `ValidateExact` it refuses
  to open instead.
- **Protected, audited journal access.** Each write consumes its single-use
  `SecurityGrant` and completes required audit before anything is appended. An
  unavailable grant store or audit dispatcher denies the write rather than
  recording unaudited state.
- **Inspectable evidence.** The canonical encoding is stable and readable, which
  is the reason to choose this adapter over SQLite for local development and
  post-mortem inspection.

## What it does not claim

The journal holds an **advisory exclusive lock** on its root and rejects a
second writer, so it claims no multi-process coordination and **ships no lease
manager**. Fencing tokens are still enforced against the persisted last-writer
generation, which protects a restarted single writer from a stale generation; it
does not make two concurrent hosts safe.

A composition that needs cross-process ownership selects
`AgentKit.Durability.Sqlite`, whose lease manager allocates generations inside
the shared database, or a distributed backend.

## Composition

```csharp
services.AddJsonDurableOperationJournal(
    new DurableJournalKey("local"),
    new JsonDurableStoreTarget(
        directoryPath: "/var/lib/myapp/durability",
        expectedStoreInstanceId: new JsonDurableStoreInstanceId(storeId),
        openMode: JsonStoreOpenMode.CreateIfMissing,
        recoveryMode: JsonStoreRecoveryMode.RecoverTornAppends));
```

The registration is keyed, because a durability profile selects its journal by
exact key and registration order must never choose a persistence target. Bounds
are validated during registration rather than on first use, so an impossible
limit fails at composition instead of mid-run.

Call `InitializeAsync` once during trusted host bootstrap before any operation
is journaled. Dispose the journal to release the advisory lock.
