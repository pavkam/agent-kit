# AgentKit.Durability.InMemory

Provide `InMemoryDurableLeaseManager`, the first-party `IDurableLeaseManager`
for one process. Select it explicitly with `AddInMemoryDurableLeaseManager()`.
Repeating this leaf is idempotent; a different lease-manager registration
remains visible so composition can reject ambiguity.

This leaf implements execution leases only. The durable journal, checkpoint
store, recovery policy, and provider-neutral coordinator described in
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

## Related projects

- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — defines
  `IDurableLeaseManager`, `IExecutionLease`, and the durable-execution value
  types this leaf implements.

## Tests and reference

- [AgentKit.Durability.InMemory.Tests](../../tests/AgentKit.Durability.InMemory.Tests/README.md)
  — focused behavior and registration tests.
- [Durable execution](../../docs/architecture/durable-execution.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
