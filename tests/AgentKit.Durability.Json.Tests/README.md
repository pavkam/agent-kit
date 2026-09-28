# AgentKit.Durability.Json.Tests

These tests run the shared
[`IDurableOperationJournal` conformance suite](../AgentKit.Conformance/) against
[`AgentKit.Durability.Json`](../../src/AgentKit.Durability.Json/), plus
adapter-specific cases for fixed-root bootstrap, manifest identity and encoding
binding, reopen persistence, compaction, torn-append recovery, and the advisory
single-writer lock.

Crash recovery and corruption rejection are proven by reading and damaging the
on-disk log directly, because the only damage a crash can cause — a record whose
terminating newline never reached disk — cannot be produced through the public
store surface.

There is no lease-manager suite here: the adapter ships no lease manager,
because a store that rejects a second writer cannot honestly coordinate
ownership between processes.
