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
[coding-harness tool profile](../../../docs/profiles/coding-harness/coding-harness-built-in-tools.md).
Also read
[workspace mutations](../../../docs/profiles/coding-harness/workspace-mutations-and-code-editing.md)
for edit/write/patch behavior or
[language services](../../../docs/profiles/coding-harness/language-services-formatters-and-watchers.md)
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
- Keep provider discovery independent from catalog selection. Every successful
  discovery transfers a fresh owned capture. Static providers share only
  explicit immutable publication/binding evidence and retain external invoker
  ownership; they do not infer aliases, filter principal-specific data, or grant
  authority. Register sources under exact typed source IDs, and preserve old
  captures when replacing registrations.
- Resolve authored toolsets through a materialized `IToolRegistrationCatalog`
  before discovery. Capture exact publications and provider bindings once at
  composition, reject missing/duplicate keys and source identity mismatches, and
  retain no runtime container. Selection preserves authored order and includes
  shared sources once; empty selection never falls back to registered tools.
  Replacement affects later compositions, while existing selections and provider
  disposal ownership remain intact.
- Retain provider and catalog captures explicitly. Acquire invoker leases only
  from the exact captured source versions; never recover a live binding from
  current DI registrations or descriptor names. Preserve requested aliases and
  catalog versions through resolution and validation, with explicit ownership
  and cleanup on capture/acquisition failure or cancellation.
- Validate every selected publication before constructing the complete ordered
  merge context and calling collision policy once. Keep competing source,
  descriptor, execution-policy, and explicit alias evidence together. Revalidate
  the policy's complete selection; it cannot invent evidence, hide a missing
  alias target, borrow unrelated toolset membership, or reorder the catalog.
  Alias choices must agree with the selected binding. Preserve empty sources,
  require one explicit merge policy, and retain capture ownership across merge
  failure or cancellation.
- Close acquisition before draining pending acquisitions, outstanding leases,
  and owned source resources. Share repeated disposal completion/failure, never
  retry cleanup implicitly, and retain host ownership of borrowed invokers. A
  caller must release its leases before awaiting capture closure on the same
  control path. Catalogs validate returned descriptor and source-version
  evidence before transferring a lease and release late acquisitions after
  closure or cancellation. One failing source cleanup must not suppress the
  other owned cleanups.
- The model requests a tool; it never executes one. Record accepted calls before
  effects. Every bounded, identified request—including pre-invocation
  rejection—produces exactly one authoritative terminal `ToolCallResult` through
  `IToolCallRecorder` and one correlated projection.
- Preflight batches, preserve source ordinals, use explicit barrier/concurrency
  rules, and publish durable results deterministically.
- Bound and normalize outputs without silently stringifying unsupported media.
  Invokers return raw attempt evidence; the executor owns normalization and
  constructs the terminal record from retained admission and acceptance
  evidence. Project the recorded result separately into a bounded
  durable/model-facing `ToolResultPart`; preserve exact status, uncertainty,
  source correlation, and every projection loss plus the captured policy
  key/version. Preserve the requested alias without fabricating resolved
  identity for unknown tools. A publication retry never invokes the tool again.
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
