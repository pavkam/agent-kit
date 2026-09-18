# AgentKit.Budgets.Json

Durable host-local storage for AgentKit budget scopes, reservations, exact
accounting, overrun holds, reconciliation, and replay receipts, written as a
newline-delimited JSON transition journal beside a small JSON manifest.

`AddJsonBudgetLedger` registers one additive `IBudgetLedger` selection without
touching the filesystem. The host supplies a fixed store root and calls
`InitializeAsync` during trusted bootstrap. Repeating the same registration is
idempotent; competing ledgers stay visible so runtime composition rejects
ambiguity.

```csharp
services.AddJsonBudgetLedger(
    new JsonBudgetLedgerTarget(
        "/var/lib/myapp/budgets",
        new JsonBudgetLedgerInstanceId(storeId),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends),
    options => options.MaximumRecordBytes = 262_144);
```

## How it stores accounting

The store is log structured. Every accounting transition is exactly one JSON
line appended and flushed to disk before the operation is acknowledged, and the
in-memory projection is mutated only after that flush returns. One indivisible
batch reservation is therefore one line carrying the whole ordered batch, its
allocated reservation identities, and their effective expiries, so recovery can
never observe a partially reserved batch.

The journal records the caller's original immutable evidence together with every
value the ledger itself chose: allocated identities, resolved effective
expiries, and the exact clock instant the transition observed. Replay re-runs
the same transition logic against those recorded values, so the recovered
projection — including monotonic ledger revisions, accounting revisions, and
overrun-hold generations — is identical to the one the writing process held.

A started reservation whose spend is still unknown is journaled at
`MarkStartedAsync` and is never released by expiry, disposal, or process loss.
It reappears from `ReadUnresolvedStartedAsync` after restart and stays charged
until `ReconcileAsync` settles, releases, or explicitly retains it.

## Durability, concurrency, and bounds

The adapter reports `Durable = true` with
`BudgetLedgerConcurrencyDomain.ProcessLocal`. Committed accounting survives
process loss, but a host-local advisory exclusive lock is held for the store's
lifetime, so a second writer on the same host fails fast rather than being
coordinated. This is single-writer durable local storage: no distributed lease,
no fencing token, and no atomicity with any external effect.

The journal is never compacted away. Accounting revisions, hold generations, and
recovery watermarks are derived from the ordered transition history, so
discarding history would change persisted identities. The only rewrite the
adapter performs is discarding one incomplete trailing append under
`JsonStoreRecoveryMode.RecoverTornAppends`;
`JsonStoreRecoveryMode.ValidateExact` reports that torn tail as unusable
evidence instead.

The manifest binds the store identity, schema version, and a fingerprint of the
effective JSON encoding contract. A root written under one contract is never
decoded under another, and initialization round-trips a representative record
before accepting the composition. Path and link checks detect ordinary
replacement; they are not operating-system confinement.

Related projects: [`AgentKit.Abstractions`](../AgentKit.Abstractions/),
[`AgentKit.Budgets`](../AgentKit.Budgets/),
[`AgentKit.Storage.Json`](../AgentKit.Storage.Json/),
[`AgentKit.Budgets.InMemory`](../AgentKit.Budgets.InMemory/),
[`AgentKit.Budgets.Sqlite`](../AgentKit.Budgets.Sqlite/), and the
[`JSON adapter tests`](../../tests/AgentKit.Budgets.Json.Tests/).
