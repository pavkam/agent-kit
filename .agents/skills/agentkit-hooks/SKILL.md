---
name: agentkit-hooks
description:
  "Design, implement, or debug AgentKit typed lifecycle hooks, dispatch,
  ordering, mutation validation, isolation, and reentrancy. Use for named hook
  boundaries; not telemetry sinks or complete strategy replacement."
---

# AgentKit Hooks

Read [AGENTS.md](../../../AGENTS.md), the
[hooks architecture](../../../docs/architecture/extensions.md), and the
[normative hooks specification](../../../docs/concepts/extensions-hooks-and-middleware.md).
When changing C#, also read the
[modern C# rules](../references/modern-csharp.md).

## Boundary

- Define one narrow hook interface and one EventArgs-derived type for each named
  boundary in AgentKit.Abstractions. AgentKit.Hooks owns dispatch, ordering,
  validation, activation, and per-run catalogs.
- Do not add a generic event-name/object hook, mutable engine view, service
  locator, ambient current run, or universal cancellation flag.
- Keep identity, causality, principal, committed state, and authority-bearing
  values read-only. Expose only boundary-specific writable values.
- Run mutating hooks sequentially, validate after every invocation, and make
  ordering deterministic. Paired after/error hooks unwind in reverse order.
- Capture immutable catalogs for in-flight work. Define lifetime, cancellation,
  deadline, failure, short-circuit, and bounded reentrancy semantics explicitly.
- Hooks never grant, widen, forge, consume, or mint authority; changed protected
  operations return through security evaluation.
- Hooks do not own telemetry. They emit bounded diagnostics through
  observability contracts, whose sinks remain immutable observers.

Use the specification's acceptance scenarios to cover ordering, invalid
mutation, scope isolation, reentrancy, unwind, failure, and security limits.
