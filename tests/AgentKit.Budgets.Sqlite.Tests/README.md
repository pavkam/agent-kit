# AgentKit.Budgets.Sqlite.Tests

These tests run the shared
[`IBudgetLedger` conformance suite](../AgentKit.Conformance/) against
[`AgentKit.Budgets.Sqlite`](../../src/AgentKit.Budgets.Sqlite/) and cover its
fixed-target bootstrap, exact schema, bounded codecs, local concurrency,
recovery, corruption handling, query plans, diagnostics, and process loss.

The separate [`ProcessHost`](../AgentKit.Budgets.Sqlite.ProcessHost/) exists
only to create a started reservation in a process that this suite terminates
abruptly.
