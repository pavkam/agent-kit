# AgentKit.Durability

Coordinate durable execution: profiles, runtime selection, recovery policy,
fenced journal access, and the execution coordinator.

Applications register keyed journals, lease managers, backends, and recovery
policies, then bind them through named durability profiles. The coordinator
activates the captured runtime, acquires execution leases, and routes work to
registered operation handlers.

## Handlers and mid-operation evidence

The coordinator owns the fenced journal, the execution lease, the captured
authorization, and checkpoint identity. A registered `IDurableOperationHandler`
performs the effect and receives a `DurableInvocationContext` whose
`Checkpoints` writer is bound to exactly that attempt: it can record a semantic
boundary or a waiting condition, and cannot write under another generation or
operation. The writer is revoked when the invocation returns. Each write is
authorized with the effect its journal method enforces (start `Create`,
checkpoint `Append`, wait `Mutate`, terminal `Append`).

First-party components journal their boundaries through `DurableBoundaryScope`
and `DurableBoundaryRegistry` in `AgentKit.Abstractions`, so a boundary's live
continuation is reachable from the handler only inside the process that prepared
it. Recovery of an operation whose terminal record is already settled returns
the recorded result and writes nothing; recovery of a started non-idempotent
operation with no terminal record requires an operator. See the
[architecture](../../docs/architecture/durable-execution.md).

## Use this project

Start with `AddAgentDurability`, `AddDurabilityProfile`, and the keyed
`AddDurableJournal` / `AddDurableLeaseManager` / `AddDurabilityBackend` helpers
in [ServiceExtensions.cs](ServiceExtensions.cs). Pair with a leaf adapter such
as `AgentKit.Durability.InMemory`, `AgentKit.Durability.Sqlite`, or
`AgentKit.Durability.Json` for journal and lease storage.

Target: **.NET 10**. See [Getting started](../../docs/getting-started.md) and
the [composition guide](../../docs/guides/composition.md).
