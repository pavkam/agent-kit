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
- `AddAgentTools()` registers `ToolCatalogCoordinator` as `IToolCatalog`,
  `DefaultToolExecutor` as `IToolExecutor`, and `IToolRunCatalogCaptureFactory`
  for run-bound captures. Applications replace individual pieces with
  `ReplaceToolCatalog<T>()`, `ReplaceToolExecutor<T>()`, or keyed registrations;
  there is no legacy `ITool` / allow-list authorizer path.
- Resolve calls against the immutable catalog snapshot sent to the model. Bound
  and validate canonical arguments before authorization or invocation.
- Compile complete canonical schemas through `IToolSchemaEngine` before exposure
  and retain exact `ICompiledToolSchema` evidence. An explicit profile declares
  dialect and keyword support; unknown keywords reject instead of silently
  weakening validation. Bound raw bytes before decoded allocation, depth, nodes,
  and total comparison work. Keep numeric comparisons exact, Unicode length
  semantics explicit, cancellation distinct from invalidity, and format
  annotations separate from typed semantic validation. Provider translation and
  raw argument parsing remain independent boundaries.
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
- Retain returned source owners before cancellation and snapshot validation.
  Reject reused captures and release all partial acquisitions after discovery
  failure. Start every source cleanup before awaiting any and preserve original
  failure followed by source-ID-ordered cleanup failures. Hold exact
  publications through merge/preflight; handoff and closure have one winner and
  never reread live metadata or dispose a transferred catalog from the old
  owner.
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
- The model requests a tool; it never executes one. Every bounded, identified
  request—including pre-invocation rejection—produces exactly one authoritative
  terminal `ToolCallResult` and one correlated projection. The executor commits
  an `AcceptedToolCall` through the run's `IToolCallRecorder` after
  authorization and before the invoker starts, and fails the call closed (typed
  `Unsupported`, `DefinitelyNotPerformed`) when that record cannot be made
  durable. It records every terminal result afterward without the caller's
  token; a failed terminal record never changes the outcome. The first-party
  `SessionToolCallRecorder` appends `ToolCallAcceptedSessionEntry` and a
  content-free `ToolCallTerminalSessionEntry` through the capability's session
  coordinator and `ToolCallSessionTarget`. Recorders are keyed by
  `ComponentKey<IToolExecutor>`; an unkeyed recorder never satisfies a keyed
  executor.
- Select execution policy by the exact captured `ToolExecutionPolicyReference`
  through `IToolExecutionPolicySelector`; never fall back to a default, a newer
  revision, or an unbound reference. A policy plans scheduling, timeout, retry
  pacing, and normalization; it never authorizes. `AddAgentTools` registers
  `DefaultToolExecutionPolicy` only for `standard@1`.
- `IToolEventSink` observes content-free `ToolEvent` values through
  `ToolEventDispatcher`, which isolates every sink failure and bounds each
  delivery. A sink can never change an outcome.
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
- Retry only when effect and side-effect certainty make it safe: read-only calls
  and mutating calls known not to have started follow ordinary policy; a
  possibly-started mutating call retries only for a declared `Idempotent` or
  `IdempotentWithKey` descriptor whose invoker implements
  `IIdempotencyEnforcingToolInvoker` and confirms the exact context. The attempt
  budget counts the first attempt (`MaximumAttempts`). No first-party tool
  declares idempotency, so none implements the confirmation; document why before
  adding one.
- The scheduler enforces the planned per-attempt `InvocationTimeout` (token
  cancellation, a bounded drain, then abandonment with `TimedOut`, retryable,
  unknown certainty); a descriptor's `ExpectedDuration` can extend it only up to
  `MaximumInvocationTimeout`. Through the capability's optional budget scope the
  runtime reserves the concurrent-call gauge, counts retries, and accounts
  successes and result bytes; a run without a budget reserves nothing, and the
  loop alone counts attempted calls.
- `AgentRunOptions.AllowedTools` narrows a run's catalog: the capture factory
  wraps the capture in `AllowListedToolCatalogCapture` (snapshot
  `IntersectWith`) before preflight or exposure, so unlisted tools are unknown
  at resolution. It only removes tools and grants nothing.
- Oversized all-text results spill to an artifact only when the keyed executor
  selected `AddToolResultSpill` and the snapshot permits externalization;
  otherwise they truncate. The terminal content keeps a bounded preview plus
  `ToolResultArtifactContent`, the spill appends no session record and records
  no reconciliation intent, and the projector renders a bounded artifact
  pointer.
- `NetworkWebSearchProvider` consumes the tool-issued search grant with required
  audit, then sends one bodyless `GET` through `INetworkNameResolver` and
  `INetworkTransport` under separate resolution and send grants with redirects
  refused; it never owns an `HttpClient`, and missing authority, enforcement, or
  audit yields `WebSearchDenied` before any I/O.
- This skill does not define authorization, approvals, grants, filesystem,
  network, or process behavior. Those owners remain independently replaceable.

Test catalog collisions, schema downgrade, snapshot resolution, preflight,
scheduling, cancellation, retries, output bounds, full-result/projection
mapping, continuation provenance, and terminal settlement.
