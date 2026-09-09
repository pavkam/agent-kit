# AgentKit.Budgets.Sqlite

Durable host-local storage for AgentKit budget scopes, reservations, exact
accounting, overrun holds, reconciliation, and replay receipts.

`AddSqliteBudgetLedger` registers one additive `IBudgetLedger` selection without
opening storage. The host supplies a fixed target in an existing directory and
calls `InitializeAsync` during trusted bootstrap. Repeating the same
registration is idempotent; competing ledgers stay visible so runtime
composition rejects ambiguity.

Initialization may create the configured database file and SQLite recovery
sidecars. It never creates parent directories. Exact validation checks the
application identity, store identity, tables, constraints, indexes, views,
triggers, and WAL mode. Path and link checks detect ordinary replacement; they
are not an operating-system confinement guarantee.

Schema creation commits before WAL establishment. If later bootstrap validation
fails, retrying the same fixed target recognizes the committed schema and
finishes validation; it never treats that known commit as an absent database.

SQLite provides durable coordination for processes sharing one host-local file.
It does not claim distributed fencing, cross-store atomicity, or authority to
open arbitrary caller paths. Evidence is versioned, bounded, integrity checked,
and decoded into validated domain values. A failure whose commit acknowledgement
is unknown must be retried with the exact semantic idempotency evidence.

Related projects: [`AgentKit.Abstractions`](../AgentKit.Abstractions/),
[`AgentKit.Budgets`](../AgentKit.Budgets/),
[`AgentKit.Budgets.InMemory`](../AgentKit.Budgets.InMemory/), and the
[`SQLite adapter tests`](../../tests/AgentKit.Budgets.Sqlite.Tests/).
