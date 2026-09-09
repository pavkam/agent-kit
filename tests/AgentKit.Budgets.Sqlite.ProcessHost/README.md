# SQLite budget process-loss helper

This non-packable test executable creates and starts one budget reservation,
persists a readiness marker, and remains alive until its parent test kills the
process. It exists solely to prove that the SQLite ledger recovers unknown
started usage after abrupt process termination. Applications must not reference
or deploy this helper.

Related projects: the [`SQLite adapter`](../../src/AgentKit.Budgets.Sqlite/) and
its [`test suite`](../AgentKit.Budgets.Sqlite.Tests/).
