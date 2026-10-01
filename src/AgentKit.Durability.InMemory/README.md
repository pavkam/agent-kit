# AgentKit.Durability.InMemory

Provide `InMemoryDurableLeaseManager`, the first-party `IDurableLeaseManager`
for one process, and `InMemoryDurableOperationJournal`, the first-party
`IDurableOperationJournal` for one process. Select them explicitly with
`AddInMemoryDurableLeaseManager(DurableLeaseManagerKey)` and
`AddInMemoryDurableOperationJournal(DurableJournalKey)`. Both registrations are
keyed, because a durability profile selects its journal and lease manager by
exact key and registration order must never choose a persistence target.
Repeating either leaf under the same key is idempotent; a registration under a
different key remains visible so composition can reject ambiguity.

This package implements execution leases and journal recording only. The
checkpoint store, recovery policy, and provider-neutral coordinator described in
[durable execution](../../docs/architecture/durable-execution.md) remain
unimplemented and are not part of this package.

## Process-local ownership only

The manager is itself the single authoritative in-process lease store: every
fencing token is allocated under its own serialization gate, so ordering among
every caller in this process is exact. It cannot coordinate across process
boundaries, because its state exists only in this process's memory. Composing
more than one engine process, or more than one instance of this manager, against
durable operations that must exclude each other does not produce mutual
exclusion — that requires a distributed backend leaf whose fencing tokens are
allocated by a real shared store instead of one process's memory.

`AcquireAsync` grants a new ownership generation when no unexpired lease exists
for the address, whether because none was ever granted or because the previous
generation expired; otherwise it returns `ExecutionLeaseHeldByAnotherWorker`
with the current owner, token, and expiry, without waiting for the caller. A
worker that still holds an unexpired lease and calls `AcquireAsync` again for
the same address receives the same busy-lease outcome as any other caller — it
is expected to use `RenewAsync` to extend its own lease instead.

`RenewAsync` extends expiry by the lease's originally requested duration,
measured from the injected clock, without allocating a new token. It succeeds
only while the presented token remains the manager's current generation for that
address; otherwise it returns `LeaseLost` with the current token when one is
recorded. Disposing a lease releases its generation immediately if it is still
current, so another worker can take over without waiting for expiry; a lease
that already lost ownership to takeover releases nothing, because a later
generation already owns the record. Renewing or disposing an already disposed
lease throws `ObjectDisposedException` or is a harmless no-op, respectively.

Expiry and elapsed-time measurement use the injected `TimeProvider` exclusively,
so takeover behavior is deterministic in tests.

## Durable journal recording

`InMemoryDurableOperationJournal` records acceptance, checkpoints, waiting, and
terminal results per `DurableOperationAddress`, and rejects a write whose
fencing token is older than the current authoritative one with
`DurableRecordFenced`. It computes `DurableOperationState` from which method
committed most recently — `Accepted` after acceptance, `EffectPending` after a
checkpoint, `Waiting` after a waiting record, and the caller-supplied terminal
state after a terminal record — and derives
`RecoveryEvidence.StartDefinitelyAbsent` and `SideEffectCertainty` from that
same computed state. A checkpoint, waiting, or terminal write against an address
with no accepted record, or a terminal write after a terminal record already
exists under a different result, returns `DurableRecordFailed`; a checkpoint or
waiting write after any terminal record does too. Repeating an equivalent
terminal write is idempotent.

## Protected journal access

Every journal operation is protected. The journal consumes the single-use
`SecurityGrant` carried by `AuthorizedDurableRequest<TRequest>` through
`ISecurityGrantStore.ValidateAndConsumeAsync`, verifies the returned
`SecurityEnforcementIntentReceipt` against the evidence it recomputed itself,
and completes required audit dispatch before it touches any record. The
committed `DurableRecorded` carries the resulting
`DurableJournalEnforcementReceipt`, so a caller can prove which grant, intent,
and audit record authorized the write.

The journal fails closed. A grant naming a different journal key, an enforcement
intent whose required fence differs from the presented token, an authorization
capture that cannot describe the operation address, an unconsumed or exhausted
grant, missing or unrelated intent evidence, and unavailable or refused audit
delivery all deny before any record changes. Writes require the caller's current
fence; an authorized evidence read is unfenced, because a recovering worker must
be able to learn what happened before it seeks ownership.

Audit records carry only bounded redacted values: the consuming audience, the
operation kind, the effect, and fingerprints of the request and the protected
resource. No payload, address text, or credential reaches an audit sink.

Because journal access is protected, the registration does not create the
security boundaries it depends on. Composition must supply `ISecurityGrantStore`
and `ISecurityAuditDispatcher` before the journal resolves.

## Related projects

- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — defines
  `IDurableLeaseManager`, `IExecutionLease`, `IDurableOperationJournal`, and the
  durable-execution value types this leaf implements.

## Tests and reference

- [AgentKit.Durability.InMemory.Tests](../../tests/AgentKit.Durability.InMemory.Tests/README.md)
  — focused behavior and registration tests.
- [Durable execution](../../docs/architecture/durable-execution.md) — intended
  ownership and contracts.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
