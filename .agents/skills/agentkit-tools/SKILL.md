---
name: agentkit-tools
description:
  "Design, implement, or debug AgentKit tool catalogs, schemas, resolution,
  scheduling, invocation, normalization, and results. Use for the tool runtime
  and feature packages; not authorization policy or low-level host effects."
---

# AgentKit Tools

Read [AGENTS.md](../../../AGENTS.md), the
[tools architecture](../../../docs/architecture/tools.md), and the normative
[tool model](../../../docs/concepts/tools-and-toolsets.md),
[call lifecycle](../../../docs/concepts/tool-call-lifecycle.md),
[scheduling rules](../../../docs/concepts/tool-scheduling-and-concurrency.md),
and [result rules](../../../docs/concepts/tool-errors-retries-and-results.md).
When changing C#, read the [modern C# rules](../references/modern-csharp.md).

When work spans protected execution end to end, also use
[agentkit-tools-and-permissions](../agentkit-tools-and-permissions/SKILL.md).
Use [agentkit-host-access](../agentkit-host-access/SKILL.md) for low-level file,
network, or process enforcement.

## Boundary

- Keep descriptors, providers, catalogs, resolvers, validators, schedulers,
  invokers, normalizers, recorders, and event sinks as separate contracts.
- AgentKit.Tools owns the optional first-party runtime. Feature packages use
  AgentKit.Tools.ToolName and depend only on abstractions for host effects.
- Resolve calls against the immutable catalog snapshot sent to the model. Bound
  and validate canonical arguments before authorization or invocation.
- The model requests a tool; it never executes one. Record accepted calls before
  effects and produce exactly one correlated terminal result.
- Preflight batches, preserve source ordinals, use explicit barrier/concurrency
  rules, and publish durable results deterministically.
- Bound and normalize outputs without silently stringifying unsupported media;
  retry only when effect and side-effect certainty make it safe.
- This skill does not define authorization, approvals, grants, filesystem,
  network, or process behavior. Those owners remain independently replaceable.

Test catalog collisions, schema downgrade, snapshot resolution, preflight,
scheduling, cancellation, retries, output bounds, and terminal settlement.
