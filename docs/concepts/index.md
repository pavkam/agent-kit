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

### Coding-harness profile

- [Coding harness execution profile](coding-harness-execution-profile.md)
  composes existing AgentKit owners into one durable interactive, batch, and RPC
  harness without inventing a god interface.
- [Coding workspaces and worktrees](coding-workspaces-and-worktrees.md) defines
  canonical project identity, provisioning, leases, readiness, reset, and safe
  removal.
- [Workspace mutations and code editing](workspace-mutations-and-code-editing.md)
  binds fuzzy edits, patches, moves, formatting, authorization, and settlement
  into one exact mutation transaction.
- [Coding-harness built-in tools](coding-harness-built-in-tools.md) specifies
  observation, search, output spill, web fetch, session-state, delegation, and
  nested orchestration details.
- [Interactive terminals and process sessions](interactive-terminals-and-process-sessions.md)
  specifies PTY ownership, byte cursors, bounded fan-out, attachment, and
  process-tree cleanup.
- [Language services, formatters, and watchers](language-services-formatters-and-watchers.md)
  makes installation, document versions, code actions, diagnostics, restart, and
  watch gaps explicit.
- [Workspace snapshots and reversion](workspace-snapshots-and-reversion.md)
  separates filesystem coverage and restoration from session, context, UI, and
  external-effect semantics.
- [Coding-harness resources and project trust](coding-harness-resources-and-project-trust.md)
  keeps discovery pure and gives instructions, variables, remote includes,
  templates, extensions, and reloads bounded provenance.
- [Coding-harness export, sharing, and control plane](coding-harness-export-sharing-and-control-plane.md)
  defines authenticated workspace routing, runtime instance ownership, versioned
  routes, reconnect, export, publication, and import.
- [Coding-harness frontends and protocol adapters](coding-harness-frontends-and-protocol-adapters.md)
  keeps TUI, IDE, batch, RPC, and agent-client protocol state as projections of
  one canonical harness.
- [Coding-harness MCP exposure](coding-harness-mcp-exposure.md) constrains
  remote names, instructions, roots, pagination, change generations, and nested
  calls.

### Messages, context, and sessions

- [Message and content model](message-and-content-model.md) defines immutable
  envelopes, role trust, typed parts, and authoritative-tool-result projection
  correlation.
- [Streaming and event protocol](streaming-and-event-protocol.md) separates live
  deltas from replayable semantic events.
- [Input admission and message queues](input-admission-and-message-queues.md)
  defines idempotent acceptance, steer/follow-up/next-run/write delivery, and
  safe promotion.
- [History validation and repair](history-validation-and-repair.md) protects
  durable truth while producing provider-compatible request views.
- [Context assembly and instructions](context-assembly-and-instructions.md)
  combines authorized history, instructions, retrieval, tools, and budgets.
- [Sessions, persistence, and branching](sessions-persistence-and-branching.md)
  defines immutable entry trees, current lane state, usage ledgers, serialized
  mutation, snapshots, and branches.
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
- [Coding-harness provider profiles](../providers/coding-harness-provider-profiles.md)
  records route-level interoperability requirements without treating one
  compatibility family as universal provider behavior.
- [Configuration and overrides](configuration-and-overrides.md) defines merge
  algebra, precedence, dynamic values, trust, and reload boundaries.
- [Execution identity and tenancy](execution-identity-and-tenancy.md) defines
  trusted ingress, subject propagation, delegation identity, and isolation.
- [Structured output](structured-output.md) defines capability-negotiated output
  modes, validation, retries, and tool-output end strategies.

### Tools, authority, and integration

- [Tools and toolsets](tools-and-toolsets.md) separates tool identity,
  discovery, resolution, schema, execution hints, dynamic availability, and
  focused read/write feature ownership.
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
                                      │       ├─> budgets
messages ───────┬─> history ─> context ──────> provider request
                ├─> streaming ────────────────┤
                └─> artifact references       ├─> output validation
admission ──────┴─> sessions ─> compaction ───┤
loop ─> protected operations ─> security ─────┘
             ├─> deferred
             └─> artifacts/file/network/process

coding harness = workspaces + mutations + terminals + language services
               + snapshots + resources + control plane + channel adapters

configuration + capabilities + typed hooks apply at documented boundaries;
observability records them without becoming a control dependency.
```

## Change discipline

A spec change MUST identify affected conformance tests and compatibility. A
behavior implemented differently from a spec is a bug until the spec is
deliberately amended. Provider quirks belong in capability profiles or adapter
documentation, never as undocumented changes to core semantics.
