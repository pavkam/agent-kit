# Design principles

**Status:** Normative foundation

## Purpose

The [architecture index](../architecture/index.md) maps these principles to
component owners, registrations, and dependency rules.

AgentKit is a composable agent runtime, not a monolithic assistant. These
principles decide ambiguous cases in the more detailed specifications.

A built `AgentEngine` is the process-level composition and may host many
independently configured agents. An agent is an immutable definition and
engine-bound runtime handle; mutable session and run state never lives on the
engine or agent object.

## Principles

### Make causality data

Runs, messages, admitted inputs, tool calls, results, approvals, goals, and
events MUST follow the
[stable identity and correlation model](message-and-content-model.md).
Relationships such as “this result answers that call” or “this prompt resumed
that run” MUST use IDs, never text matching, array adjacency alone, or ambient
state.

Every domain identity MUST have a dedicated immutable value type, normally a
`readonly record struct`. Public contracts MUST NOT pass an agent, session, run,
turn, message, tool call, goal, or operation identity as a raw string, GUID, or
integer. Identifier creation that affects deterministic behavior MUST use an
injected generator.

### Separate acceptance from execution

[Accepting input is not the same operation as promoting it](input-admission-and-message-queues.md)
into a turn. Recording a tool call is not
[executing it](tool-call-lifecycle.md). Ending model generation is not
[settling a run](run-lifecycle-and-settlement.md). The runtime MUST expose these
boundaries because they are where idempotency, recovery, and concurrency are won
or lost.

### Preserve more than the lowest common denominator

Portable concepts MUST be normalized. Under the
[provider capability contract](model-providers-and-capabilities.md),
provider-specific IDs, finish reasons, reasoning signatures, citations, safety
information, unknown content, and raw diagnostics remain available as typed
extension data. Provider neutrality does not mean convenient data loss.

### Prefer explicit state machines

All long-lived or multi-step behavior MUST define states, valid transitions,
terminal outcomes, and recovery behavior, as the
[agent loop state machine](agent-loop-state-machine.md) does for a run. A
`while (true)` may implement a state machine internally; it MUST NOT be the only
specification of one.

### One owner per policy

The loop coordinates work. It MUST NOT secretly own provider selection,
authorization, retry policy, persistence, compaction, memory, or queueing. The
[dependency boundaries](architecture-and-dependency-boundaries.md) give each
policy one replaceable owner, and ownership of retry, timeout, and cancellation
MUST be documented at every boundary.

### Be deterministic where the user can observe it

Concurrent work MAY complete nondeterministically. Persisted messages, returned
results, security evaluation, hook ordering, and replay MUST have a
deterministic order independent of completion timing; the
[tool scheduler](tool-scheduling-and-concurrency.md) makes this distinction
explicit for execution and publication order.

### Fail closed before side effects

Unknown tools, invalid arguments, missing policy context, stale approval,
unsupported provider capability, and malformed protocol messages MUST follow the
[fail-closed authority contract](permissions-approvals-and-trust.md) before
execution. Untrusted descriptions or model output MUST NOT grant authority.

### Make replacement real

An extension point is credible only when it has a narrow contract, at least two
plausible implementations, a declared lifecycle, and
[conformance tests](testing-and-evaluation.md). Core packages MUST depend
inward; integrations MUST remain leaves.

### Configure every choice, default every mechanism

Every behaviorally meaningful mechanism and policy MUST have one explicit
configuration seam: DI replacement, typed options, engine configuration, agent
definition, or run override. The owning first-party feature MUST provide a
sensible documented default through a replaceable registration when a safe
general default exists. Credentials, remote endpoints, durable storage targets,
and granted authority are external facts; the framework MUST require them
explicitly rather than inventing plausible-looking values.

### Keep history honest

Durable history is an append-oriented record of what occurred. The
[derived working context](context-assembly-and-instructions.md) may be filtered,
summarized, repaired, or translated for a request, but those transformations
MUST NOT silently rewrite durable history.

### Treat live and durable events differently

Fine-grained deltas are useful to a live UI and ruin an event log. The
[streaming protocol](streaming-and-event-protocol.md) distinguishes replayable
semantic events from ephemeral stream updates. A durable checkpoint MUST be
sufficient to reconstruct stable state without replaying every token.

### Settlement is stronger than completion

A model turn can end while listeners, tool tasks, retries, compaction, or
persistence still run. Under the
[settlement contract](run-lifecycle-and-settlement.md), “settled” means no
run-owned work can still alter the result. APIs MUST say whether they await
generation, turn completion, run completion, or settlement.

### Limits are outcomes, not accidents

Turns, tokens, requests, elapsed time, tool calls, cost, context, concurrency,
and queued work follow the [usage-budget contract](usage-limits-and-budgets.md).
Reaching a configured limit produces a typed outcome and telemetry; it MUST NOT
masquerade as an arbitrary provider failure.

## Rejected shapes

- A universal `IPlugin` with discovery, policy, execution, and observation
  methods.
- A mutable bag-of-dictionaries message model.
- A queue represented as `List<string>`.
- Tool execution directly from provider callbacks.
- Retrying a mutating operation because an exception “looks transient.”
- Calling event observers inline with authority to break the run.
- Treating model-generated or retrieved text as trusted instructions.

## Acceptance criteria

- Every public subsystem spec names its owner, lifecycle, cancellation, and
  ordering behavior.
- Every accepted protected effect can be traced to a run, causal operation,
  security request and grant, principal, and enforcing component.
- Replaying durable state yields the same semantic order even if original work
  ran concurrently.
- Replacing a default implementation requires DI configuration, not internal
  access or static state.

## Related specifications

- [Architecture and dependency boundaries](architecture-and-dependency-boundaries.md)
- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Testing and evaluation](testing-and-evaluation.md)
