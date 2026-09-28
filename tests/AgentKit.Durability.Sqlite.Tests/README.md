# AgentKit.Durability.Sqlite.Tests

These tests run the shared
[`IDurableOperationJournal` and `IDurableLeaseManager` conformance suites](../AgentKit.Conformance/)
against [`AgentKit.Durability.Sqlite`](../../src/AgentKit.Durability.Sqlite/),
plus adapter-specific cases for fixed-target bootstrap, exact schema validation,
bounded codecs, database-allocated fencing tokens, reopen persistence, and keyed
registration.

Because this adapter claims durability, the journal suite also exercises its
reopen case: state acknowledged by one instance must be observed by a second
instance opened over the same database.
