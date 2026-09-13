# AgentKit.Durability.InMemory.Tests

This project verifies `InMemoryDurableLeaseManager` and `InMemoryExecutionLease`
directly: acquisition, takeover after expiry, busy-lease refusal, renewal,
release, cancellation, deterministic clock behavior, and registration. It also
verifies `InMemoryDurableOperationJournal`: acceptance, checkpoint, and terminal
recording; fencing rejection; binding-mismatch and terminal-conflict failures;
idempotent terminal replay; evidence assembly across the full lifecycle; and
registration. There is no other `IDurableLeaseManager` or
`IDurableOperationJournal` backend yet, so this suite owns its own behavioral
fixtures rather than a shared reusable conformance suite.
