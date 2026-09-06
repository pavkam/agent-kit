# Design principles

**Status:** Normative foundation  
**Depends on:** [Research provenance](research-provenance.md)

## Purpose

AgentKit is a composable agent runtime, not a monolithic assistant. These
principles decide ambiguous cases in the more detailed specifications.

## Principles

### Make causality data

Runs, messages, admitted inputs, tool calls, results, approvals, goals, and
events MUST have stable identities. Relationships such as “this result answers
that call” or “this prompt resumed that run” MUST use IDs, never text matching,
array adjacency alone, or ambient state.

### Separate acceptance from execution

Accepting input is not the same operation as promoting it into a turn. Recording
a tool call is not executing it. Ending model generation is not settling a run.
The runtime MUST expose these boundaries because they are where idempotency,
recovery, and concurrency are won or lost.

### Preserve more than the lowest common denominator

Portable concepts MUST be normalized. Provider-specific IDs, finish reasons,
reasoning signatures, citations, safety information, unknown content, and raw
diagnostics MUST remain available as typed extension data. Provider neutrality
does not mean convenient data loss.

### Prefer explicit state machines

All long-lived or multi-step behavior MUST define states, valid transitions,
terminal outcomes, and recovery behavior. A `while (true)` may implement a state
machine internally; it MUST NOT be the only specification of one.

### One owner per policy

The loop coordinates work. It MUST NOT secretly own provider selection,
authorization, retry policy, persistence, compaction, memory, or queueing. Each
policy has one replaceable owner, and ownership of retry, timeout, and
cancellation MUST be documented at every boundary.

### Be deterministic where the user can observe it

Concurrent work MAY complete nondeterministically. Persisted messages, returned
results, permission evaluation, middleware ordering, and replay MUST have a
deterministic order independent of completion timing.

### Fail closed before side effects

Unknown tools, invalid arguments, missing policy context, stale approval,
unsupported provider capability, and malformed protocol messages MUST fail
before execution. Untrusted descriptions or model output MUST NOT grant
authority.

### Make replacement real

An extension point is credible only when it has a narrow contract, at least two
plausible implementations, a declared lifecycle, and conformance tests. Core
packages MUST depend inward; integrations MUST remain leaves.

### Keep history honest

Durable history is an append-oriented record of what occurred. Derived working
context MAY be filtered, summarized, repaired, or translated for a request, but
those transformations MUST NOT silently rewrite durable history.

### Treat live and durable events differently

Fine-grained deltas are useful to a live UI and ruin an event log. The runtime
MUST distinguish replayable semantic events from ephemeral stream updates. A
durable checkpoint MUST be sufficient to reconstruct stable state without
replaying every token.

### Settlement is stronger than completion

A model turn can end while listeners, tool tasks, retries, compaction, or
persistence still run. “Settled” means no run-owned work can still alter the
result. APIs MUST say whether they await generation, turn completion, run
completion, or settlement.

### Limits are outcomes, not accidents

Turns, tokens, requests, elapsed time, tool calls, cost, context, concurrency,
and queued work MUST be bounded. Reaching a configured limit produces a typed
outcome and telemetry; it MUST NOT masquerade as an arbitrary provider failure.

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
- Every accepted side effect can be traced to a run, message, tool call,
  permission decision, and principal.
- Replaying durable state yields the same semantic order even if original work
  ran concurrently.
- Replacing a default implementation requires DI configuration, not internal
  access or static state.

## Related specifications

- [Architecture and dependency boundaries](architecture-and-dependency-boundaries.md)
- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Testing and evaluation](testing-and-evaluation.md)
