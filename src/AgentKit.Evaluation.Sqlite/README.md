# AgentKit.Evaluation.Sqlite

Durable, host-local SQLite storage for AgentKit evaluation results.

`AddSqliteEvaluationResultStore(EvaluationResultStoreKey, SqliteEvaluationStoreTarget)`
registers one keyed `IEvaluationResultStore` over an explicit, host-authorized
database file. The host chooses the file; nothing registers it implicitly.

## Durability

Every append runs in an immediate transaction that reads the stored state
through the shared planner and writes the result, so run pinning, idempotent
replay, and the identity-conflict checks are atomic even across processes
sharing the file. An acknowledged append is committed and survives process loss
and reopen. The database is bound to its expected instance identity and a known
schema version; a missing schema is created only when the target allows the
package migration, and an unexpected schema is refused instead of altered.

A stored result is a bounded projection: identities, manifest, usage, latency,
trace identity, evaluator outcomes with their safe evidence, and diagnostics.
Prompts, model output, and tool data are never persisted.

## Limits

SQLite provides durable local storage only. It implies no distributed lease,
fencing, or atomicity with report exporters. A writer that cannot take the
database write lock within the configured wait receives a typed unavailable
rejection.
