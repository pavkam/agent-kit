# Agent runtime

**Role:** Coordinate one run without absorbing the responsibilities of its
collaborators.

AgentKit.Loop contains the first-party agentic loop. The loop contract and all
provider-neutral run values live in AgentKit.Abstractions. AgentEngine resolves
the configured loop; it does not contain privileged orchestration behavior.

The loop owns control flow: when input is promoted, when context is prepared,
when a model is called, when tools may run, whether another turn is required,
and when the run is truly settled. Session coordination remains in
AgentKit.Session.

## State and lifecycle

A run moves through explicit phases for input admission, turn preparation, model
streaming, assistant commit, tool recording and execution, completion,
cancellation or failure, and settlement. State transitions are observable and
validated. A run reaches exactly one terminal outcome and one settlement event.

The runtime distinguishes generation complete, turn complete, run complete, and
run settled. The normal high-level operation waits for settlement. A stream may
expose earlier activity, but it cannot claim the session is idle while required
persistence, recovery, middleware, or event work is unfinished.

## Coordination

For each turn, the runtime:

1. asks the I/O coordinator to promote eligible input at a safe boundary;
2. asks the context component for an immutable provider-ready request;
3. reserves budget and asks the provider runtime to select and execute one
   concrete provider attempt;
4. validates and commits the assistant response;
5. passes accepted tool calls through the tool and permission components;
6. commits terminal tool results in deterministic order; and
7. applies continuation and stop policy.

The runtime does not admit input, publish output, select history items, select a
model, translate provider wire formats, authorize tools, or implement storage.
It sequences the components that do.

## Limits and resilience

Budgets cover turns, provider requests, tokens, cost, tool calls, retries,
elapsed time, context size, queue capacity, retrieval, and buffered output.
Concurrent work reserves capacity atomically before it starts. Limit exhaustion
is a typed run outcome and preserves any truthful partial output or side-effect
certainty.

Cancellation records its source, stops new work, propagates through every
awaitable boundary, and drains owned operations within bounded settlement time.
Provider retries belong above a single-attempt adapter. Tool retries belong to
the tool pipeline. Neither may repeat work when visible output or an uncertain
side effect makes repetition unsafe.

## Replacement

The loop is replaceable as a whole. A custom loop receives the same explicit
collaborators and must preserve public lifecycle, correlation, ordering,
cancellation, and terminal-result behavior. It cannot use the dependency
container as a service locator or bypass permission and durability boundaries.

AddAgentLoop registers the first-party implementation as a singular, replaceable
service. A custom implementation may use the neutral registration surface
without referencing AgentKit.Loop. Both run through the same loop conformance
suite.

## Related concept specifications

- [Agent loop state machine](../concepts/agent-loop-state-machine.md)
- [Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md)
- [Usage limits and budgets](../concepts/usage-limits-and-budgets.md)
- [Cancellation, timeouts, and resilience](../concepts/cancellation-timeouts-and-resilience.md)
