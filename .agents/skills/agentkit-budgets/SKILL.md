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
3. Reserve atomically before concurrent work, commit actual usage afterward, and
   release excess or uncommitted capacity exactly once.
4. Enforce every applicable shared scope and preflight indivisible batches so an
   over-limit batch starts no effects.
5. Preserve measured, provider-reported, estimated, and unknown provenance.
   Provider corrections replace provisional accounting rather than double-count.
6. Return typed exhaustion with boundary and partial-effect certainty. Budget
   events observe immutable facts and cannot call back into the authority.

Verify concurrent last-slot reservations, parent/child enforcement, units, batch
rejection, release and disposal, overrun policy, provider corrections, unknown
pricing, typed partial outcomes, and replacement through DI.
