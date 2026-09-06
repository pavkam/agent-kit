# Durable execution

**Role:** Resume useful work after process loss without replaying unsafe
effects.

Durability is an optional capability with a stable core boundary. AgentKit
defines recoverable operations and evidence; leaf adapters map them to workflow
engines or durable task systems. The core does not depend on a particular
backend SDK, and an in-memory run never pretends to provide crash recovery.

Durability contracts live in AgentKit.Abstractions. Concrete integrations use
AgentKit.Durability.BackendName, such as a future Temporal or Restate package.
They register durable operation ownership without changing AgentEngine or the
loop contract.

## Durable operations

A recoverable operation has stable operation and idempotency identities, causal
run and turn references, versioned serializable input and result, declared retry
and timeout ownership, cancellation behavior, side-effect classification, and a
checkpoint or terminal record.

Model requests, tool calls, compaction, approval waits, and selected middleware
may become durable operations. Pure deterministic preparation normally replays
from captured manifests rather than serializing the runtime object graph.

Useful checkpoints occur after admission and promotion, context manifest
creation, provider terminal validation, tool-call recording, every terminal tool
result, message commit, compaction activation, and settlement. High- frequency
stream deltas are not required to reconstruct stable state.

## Recovery

Recovery follows evidence. Work definitely not started may begin. An operation
with a supported idempotency key may be reconciled or retried. A terminal result
whose commit is missing may be committed without reinvocation. Work accepted by
an external durable owner resumes through that owner.

A started non-idempotent operation with unknown outcome is not retried. It
requires reconciliation or operator action. Exactly-once is not achieved by
adding optimism to a retry loop, darling.

## Determinism and versioning

Replay uses captured configuration and catalog versions plus injected time,
identities, and randomness. It performs no live container discovery or ambient
environment reads. Schema and operation changes require migration, pinned old
behavior, or a typed incompatibility result.

## Distributed ownership

Distributed execution has one authoritative owner per session or operation.
Leases include expiry, renewal, owner identity, and a monotonically increasing
fencing token. Durable writes verify the active token so a stale worker cannot
append after takeover.

Wake signals are hints and may be duplicated or lost. Durable admitted input is
the source of truth. Settlement is complete only when run-owned work is
terminal, safely handed off, or durably represented for recovery.

## Related concept specifications

- [Durable execution and recovery](../concepts/durable-execution-and-recovery.md)
- [Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md)
- [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)
