# AgentKit.Budgets.InMemory.Tests

This project runs the reusable `IBudgetLedger` conformance suite against the
in-memory adapter and verifies its structured logs, activities, and bounded
metrics. The tests use deterministic identity, dimension, clock, cancellation,
and failure collaborators; they require no external services.
