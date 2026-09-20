# AgentKit concept specifications

## Status and intent

These documents are the normative design input for implementing AgentKit. They
define provider-neutral .NET 10 contracts and specify observable behavior rather
than prescribing one implementation architecture or API.

The key words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY**
are to be interpreted as requirements. A future implementation may depart from a
SHOULD only when the reason and compatibility impact are recorded. Anything not
stated here remains a design choice; it is not permission to violate repository
invariants in [AGENTS.md](../../AGENTS.md).

Each specification contains acceptance scenarios suitable for conformance tests.
Public names and C# sketches are provisional until an API review accepts them,
while the behavioral invariants are normative.

Product compositions belong under [application profiles](../profiles/index.md),
not in this normative core. A profile may require several AgentKit capabilities,
but it cannot make its frontend, deployment, workspace, or orchestration policy
a responsibility of the framework.

## Concept map

There is no canonical reading order. Start with the concept being implemented,
then follow its dependencies and related specifications. This index is the
stitching point; the filenames deliberately carry no sequence.

### Runtime and composition

- [Design principles](design-principles.md) defines the invariants used to
  resolve ambiguity across the library.
- [Architecture and dependency boundaries](architecture-and-dependency-boundaries.md)
  separates contracts, runtime behavior, policies, state, and integrations.
- [Agent definition and run context](agent-definition-and-run-context.md)
  separates reusable configuration from mutable execution state.
- [Agent loop state machine](agent-loop-state-machine.md) specifies turn
  coordination and terminal behavior.
- [Run lifecycle and settlement](run-lifecycle-and-settlement.md) defines when
  generation, turns, runs, and run-owned work are actually finished.
- [Public API and dependency injection](public-api-and-dependency-injection.md)
  turns those boundaries into replaceable .NET contracts and registrations.

### Messages, context, and sessions

- [Message and content model](message-and-content-model.md) defines immutable
  envelopes, role trust, typed parts, and authoritative-tool-result projection
  correlation.
- [Streaming and event protocol](streaming-and-event-protocol.md) separates live
  deltas from replayable semantic events.
- [Input admission and message queues](input-admission-and-message-queues.md)
  defines idempotent acceptance, steering/follow-up delivery, and safe
  promotion.
- [History validation and repair](history-validation-and-repair.md) protects
  durable truth while producing provider-compatible request views.
- [Context assembly and instructions](context-assembly-and-instructions.md)
  combines authorized history, instructions, retrieval, tools, and budgets.
- [Sessions, persistence, and branching](sessions-persistence-and-branching.md)
  defines immutable entry trees, current lane state, usage ledgers, serialized
  mutation, snapshots, and branches.
- [Session execution lanes](session-execution-lanes.md) defines one-active-run
  lane ownership and the durable abort marker exposed by loaded run state.
- [Context compaction](context-compaction.md) bounds working context without
  deleting or rewriting canonical history.
- [Memory, retrieval, and storage](memory-retrieval-and-storage.md) keeps
  conversation history, durable memory, documents, embeddings, indexes, and
  retrieval as separate extension axes.
- [Artifact and content storage](artifact-and-content-storage.md) owns durable
  binary and generated content referenced by messages, tools, media, and evals.

### Models, configuration, and output

- [Model providers and capabilities](model-providers-and-capabilities.md)
  distinguishes providers, service surfaces, endpoint/account bindings,
  deployments, models, response multiplicity, usage evidence, and capabilities.
- [Provider request pipeline](provider-request-pipeline.md) owns translation,
  authentication, transport, stream parsing, and provider error normalization.
- [Configuration and overrides](configuration-and-overrides.md) defines merge
  algebra, precedence, dynamic values, trust, and reload boundaries.
- [Execution identity and tenancy](execution-identity-and-tenancy.md) defines
  trusted ingress, subject propagation, delegation identity, and isolation.
- [Structured output](structured-output.md) defines capability-negotiated output
  modes, validation, retries, and tool-output end strategies.

### Tools, authority, and integration

- [Tools and toolsets](tools-and-toolsets.md) separates tool identity,
  discovery, resolution, schema, execution hints, dynamic availability, and
  focused observation and mutation feature ownership.
- [Tool-call lifecycle](tool-call-lifecycle.md) specifies the complete path from
  model request to one authoritative terminal result and bounded history
  projection.
- [Tool scheduling and concurrency](tool-scheduling-and-concurrency.md) defines
  barrier segments, preflight, deterministic publication, and cancellation.
- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
  separates correctable model errors, terminal failures, unsafe retries, full
  execution records, and loss-aware model projections.
- [Permissions, approvals, and trust](permissions-approvals-and-trust.md) is the
  fail-closed authority boundary for every operation.
- [Deferred operations and human-in-the-loop](deferred-and-human-in-the-loop.md)
  models approval, external execution, suspension, and later resolution for any
  protected operation.
- [MCP integration](mcp-integration.md) keeps MCP lifecycle, transport,
  primitives, correlation, and host policy in a leaf integration.

### Protected host boundaries

- [File-system access and bounds](file-system-access-and-bounds.md) defines path
  identity, bounded reads, explicit write dispositions, atomicity, and
  separately authorized secondary effects.
- [Network access and egress](network-access-and-egress.md) defines canonical
  destinations, egress authorization, redirect and rebinding rules, streaming
  bounds, and retry uncertainty.
- [Process execution and sandboxing](process-execution-and-sandboxing.md)
  defines executable resolution, environment projection, sandbox limits, output
  streaming, termination, and side-effect certainty.

### Operational behavior

- [Usage limits and budgets](usage-limits-and-budgets.md) defines accounting,
  reservation, hierarchical limits, and typed limit outcomes.
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
  defines propagation, uncertain side effects, provider retry, and settlement.
- [Extensions, hooks, and middleware](extensions-hooks-and-middleware.md)
  specifies stage-valid typed points, ordering strength, monotonic failure
  policy, rollback-safe isolation, and reentrancy.
- [Durable execution and recovery](durable-execution-and-recovery.md) defines
  checkpoints, replay, idempotency, leases, fencing, and reconciliation.
- [Observability and audit](observability-and-audit.md) defines traces, metrics,
  logs, redaction, and security audit records.
- [Goals and multi-agent delegation](goals-and-multi-agent-delegation.md) models
  goals, attempts, scoped authority, child work, joins, and durable handoffs.
- [Error taxonomy](error-taxonomy.md) provides stable categories, retryability,
  and side-effect certainty across every subsystem.
- [Testing and evaluation](testing-and-evaluation.md) defines shared conformance
  suites, deterministic fault injection, protocol tests, and model evaluations.

## Dependency map

```text
trusted ingress ─> identity ─> admission ─> loop ─> settlement
messages ───────┬─> sessions ─> admission
                │      └─> compaction ─┐
                ├─> history ──────────┴─> context ─> provider request
                ├─> streaming ─────────────────────> output publication
                └─> artifact references ───────────> artifacts

loop ─┬─> budgets
      └─> protected request

protected request ─> security ─┬─> tools/file/network/process
                               └─> deferred resolution

configuration + capabilities + typed hooks apply at documented boundaries;
observability records them without becoming a control dependency.
```

## Change discipline

A spec change MUST identify affected conformance tests and compatibility. A
behavior implemented differently from a spec is a bug until the spec is
deliberately amended. Provider quirks belong in capability profiles or adapter
documentation, never as undocumented changes to core semantics.
