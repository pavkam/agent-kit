# AgentKit.Budgets.Json.Tests

These tests run the shared
[`IBudgetLedger` conformance suite](../AgentKit.Conformance/) against
[`AgentKit.Budgets.Json`](../../src/AgentKit.Budgets.Json/) and add the
durability cases the shared suite cannot observe, because it never reopens a
ledger: retention of a started reservation with unknown spend across disposal,
exact batch-receipt replay after reopening, and torn-append handling under both
recovery modes.

They also cover the compile-linked
[`AgentKit.Budgets.Storage.Shared`](../../src/AgentKit.Budgets.Storage.Shared/)
scope-creation transition, which this leaf shares with the SQLite adapter.
