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

For coding-host built-ins read the
[coding-harness tool profile](../../../docs/concepts/coding-harness-built-in-tools.md).
Also read
[workspace mutations](../../../docs/concepts/workspace-mutations-and-code-editing.md)
for edit/write/patch behavior or
[language services](../../../docs/concepts/language-services-formatters-and-watchers.md)
for LSP, formatter, and code-action tools.

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
  effects. Every bounded, identified request—including pre-invocation
  rejection—produces exactly one authoritative terminal `ToolCallResult` through
  `IToolCallRecorder` and one correlated projection.
- Preflight batches, preserve source ordinals, use explicit barrier/concurrency
  rules, and publish durable results deterministically.
- Bound and normalize outputs without silently stringifying unsupported media.
  Project the recorded result separately into a bounded durable/model-facing
  `ToolResultPart`; preserve exact status, uncertainty, source correlation, and
  every projection loss plus the captured policy key/version. Preserve the
  requested alias without fabricating resolved identity for unknown tools. A
  publication retry never invokes the tool again.
- Keep first-party file tools in `AgentKit.Tools.Read` and
  `AgentKit.Tools.Write`. Read line windows are incremental model-facing
  projections over host-bounded byte streams; writes require an explicit safe
  disposition and never hide parent-directory creation.
- Retry only when effect and side-effect certainty make it safe.
- This skill does not define authorization, approvals, grants, filesystem,
  network, or process behavior. Those owners remain independently replaceable.

Test catalog collisions, schema downgrade, snapshot resolution, preflight,
scheduling, cancellation, retries, output bounds, full-result/projection
mapping, continuation provenance, and terminal settlement.
