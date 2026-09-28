# AgentKit.Durability

Coordinate durable execution: profiles, runtime selection, recovery policy,
fenced journal access, and the execution coordinator.

Applications register keyed journals, lease managers, backends, and recovery
policies, then bind them through named durability profiles. The coordinator
activates the captured runtime, acquires execution leases, and routes work to
registered operation handlers.

## Use this project

Start with `AddAgentDurability`, `AddDurabilityProfile`, and the keyed
`AddDurableJournal` / `AddDurableLeaseManager` / `AddDurabilityBackend` helpers
in [ServiceExtensions.cs](ServiceExtensions.cs). Pair with a leaf adapter such
as `AgentKit.Durability.InMemory`, `AgentKit.Durability.Sqlite`, or
`AgentKit.Durability.Json` for journal and lease storage.

Target: **.NET 10**. See [Getting started](../../docs/getting-started.md) and
the [composition guide](../../docs/guides/composition.md).
