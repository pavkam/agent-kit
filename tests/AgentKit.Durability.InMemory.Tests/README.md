# AgentKit.Durability.InMemory.Tests

This project verifies `InMemoryDurableLeaseManager` and `InMemoryExecutionLease`
directly: acquisition, takeover after expiry, busy-lease refusal, renewal,
release, cancellation, deterministic clock behavior, and registration. There is
no other `IDurableLeaseManager` backend yet, so this suite owns its own
behavioral fixtures rather than a shared reusable conformance suite.
