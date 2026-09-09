---
name: agentkit-budgets
description:
  "Implement or debug AgentKit.Budgets hierarchy, atomic reservations,
  accounting, and typed limit outcomes. Use for shared limits across concurrent
  work; not for component-local estimation policy, provider pricing integration,
  or loop continuation decisions."
---

# AgentKit Budgets

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read [budgets and limits architecture](../../../docs/architecture/budgets.md)
  for ownership, contracts, hierarchy, DI, and dependency direction.
- Read the normative
  [usage limits and budgets](../../../docs/concepts/usage-limits-and-budgets.md)
  specification for dimensions, accounting, enforcement, and outcomes.
- Read
  [observability and audit](../../../docs/concepts/observability-and-audit.md)
  only when changing usage provenance or budget events.

## Working rules

1. Keep contracts in `AgentKit.Abstractions` and the common authority in
   `AgentKit.Budgets`. Consumers estimate work and react to outcomes.
2. Use one hierarchy of host, tenant, principal, agent, session, run, and
   operation scopes. Do not maintain private counters for shared dimensions.
3. Reserve before work and mark started before an effect. Dispose only unstarted
   capacity; unknown started spend remains unresolved or explicitly estimated
   until reconciliation. Record actual overrun fully and apply corrections once.
4. Apply each dimension's declared aggregation to amounts in its declared unit.
   Sum and duration add usage; maximum compares live, committed, and candidate
   maxima; concurrent gauges sum live capacity-retaining amounts and retain no
   committed balance after proven completion or release. Never substitute row
   count or a different aggregation as a fallback. Convert bounded decimal row
   values exactly and aggregate with `BudgetQuantity`; never round, clamp, or
   fall back to decimal arithmetic for snapshot totals or limit observations.
5. Enforce every shared scope and reserve every dimension of an indivisible
   batch atomically or none. Transfer/subdivide reserved capacity rather than
   charging the same work again at a lower layer.
6. Preserve measured, provider-reported, estimated, and unknown provenance.
   Provider corrections replace provisional accounting rather than double-count.
7. Return typed exhaustion with boundary and partial-effect certainty. Budget
   events observe immutable facts and cannot call back into the authority.
8. Validate live capability bindings against tenant, principal, operation, and
   active run. Before-run and after-run addresses omit an active run; a causal
   run identifier never reopens settled capacity. New after-run work receives a
   child operation scope under an authorized non-run parent.

Verify concurrent last-slot reservations, parent/child enforcement, units, batch
rejection, release and disposal, overrun policy, provider corrections, unknown
pricing, typed partial outcomes, and replacement through DI.
