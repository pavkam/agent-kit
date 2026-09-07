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

- Define one narrow hook interface, one EventArgs-derived type, and one closed
  point definition for each named boundary. Built-in contracts live in
  AgentKit.Abstractions; third-party contracts may live in their own abstraction
  assembly. AgentKit.Hooks owns the typed dispatch kernel, ordering, activation,
  and catalog mechanics; owning features supply closed point definitions,
  validators, and optional convenience adapters.
- Let third-party features add typed point definitions additively without
  editing the central kernel. Do not add a generic event-name/object hook,
  arbitrary string/object dispatch, mutable engine view, service locator,
  ambient current run, or universal cancellation flag.
- Keep the shared event-argument base limited to point, dispatch, causality, and
  timing. Derived arguments carry only identities established at that stage.
  Distinguish point, registration, dispatch, and individual invocation
  identities; never fabricate `AgentId`, `SessionId`, or `RunId`.
- Keep identity, causality, principal, committed state, and authority-bearing
  values read-only. Expose only boundary-specific writable values.
- Run mutating hooks sequentially, validate after every invocation, and make
  ordering deterministic. `Before`/`After` references are soft when absent;
  `DependsOn`/`Requires` references are hard. Scope constraints to one point,
  profile, and catalog; treat `First` and `Last` as singleton anchors; reject
  self-reference, contradictions, and cycles. Paired after/error hooks unwind in
  reverse order.
- Capture immutable catalogs for in-flight work at their declared scope. Define
  lifetime, cancellation, deadline, failure, short-circuit, and bounded
  reentrancy semantics explicitly.
- Resolve each invocation's failure policy as the strictest of the point
  invariant, host minimum, profile default, current registration request, and
  narrower-scope tightening. Transform and security points fail the operation.
  Isolate only observation/read-only points and roll back or stage any permitted
  diagnostic mutation before continuing.
- Hooks never grant, widen, forge, consume, or mint authority; changed protected
  operations return through security evaluation.
- Hooks do not own telemetry. They emit bounded diagnostics through
  observability contracts, whose sinks remain immutable observers.

Timeout does not prove an in-process hook stopped. Do not restore shared event
arguments and continue while timed-out code can still mutate them. Drain to
quiescence or fail with explicit ownership; hard isolation requires a host
execution boundary.

Use the specification's acceptance scenarios to cover stage identity, typed
third-party points, ordering, invalid mutation, scope isolation, reentrancy,
unwind, failure-policy monotonicity, isolation rollback, and security limits.
