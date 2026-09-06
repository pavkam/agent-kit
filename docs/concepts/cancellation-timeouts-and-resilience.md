# Cancellation, timeouts, and resilience

**Status:** Normative  
**Depends on:** [Run lifecycle](run-lifecycle-and-settlement.md),
[error taxonomy](error-taxonomy.md)

## Purpose

Cancellation stops intent; it does not travel backward through time and undo
side effects. Resilience must preserve that truth across providers, tools,
queues, stores, and middleware.

## Cancellation reasons

The runtime MUST distinguish:

- caller cancellation;
- host shutdown;
- run deadline;
- operation timeout;
- policy stop;
- budget limit;
- superseded/interrupt request; and
- dependency failure cancellation.

An internal cancellation token MAY carry all of these, but the terminal result
and events MUST retain the normalized reason.

## Propagation

Every public asynchronous API accepts `CancellationToken`. The run creates a
linked token for caller, deadline, host, and internal stop sources and passes it
through every await, stream, provider attempt, tool, store, queue, middleware,
and observer operation.

Code MUST NOT translate every `OperationCanceledException` into caller
cancellation. It compares the relevant tokens and deadlines to choose the
correct terminal reason.

## Timeouts

Timeouts are named per boundary: queue admission, session lease, context
preparation, provider connect/headers/idle/overall, tool invocation, approval,
store operation, event delivery, and settlement drain. They use `TimeProvider`
and must be cancellable and observable.

A timeout result states whether the operation definitely did not start,
definitely completed, or may have produced side effects. Unknown outcome is a
first-class state.

## Tool cancellation

On cancellation the scheduler stops starting calls, signals running calls, and
waits a bounded drain period. Asynchronous cooperative tasks SHOULD settle.
Synchronous, process, network, or remote tasks may continue despite local
cancellation; after the drain deadline they are marked interrupted with unknown
effect unless an external idempotency/status API proves otherwise.

Results arriving after terminal interruption are ignored for model context and
recorded as late diagnostics or reconciliation input. They MUST NOT create a
second terminal tool result.

## Provider retry

A provider resilience policy above the adapter classifies normalized errors and
decides retry, fallback, or fail. Retry MUST consider:

- whether any output became visible or was committed;
- provider idempotency and request status;
- retry hint and rate-limit reset;
- remaining requests, tokens, cost, and deadline;
- provider/model compatibility with current history; and
- configured maximum attempts.

Backoff is bounded exponential delay with injectable jitter by default. It MUST
honor cancellation. Authentication, authorization, invalid request, unsupported
capability, and protocol violations are not transient by default.

## Partial streams

After visible partial output, transparent retry is forbidden. The runtime
commits an interrupted partial message or discards the candidate according to
documented history policy, emits failure, and lets the loop decide whether a new
request with repaired context is safe.

## Circuit breaking and fallback

Circuit breakers SHOULD key by provider endpoint/deployment and failure class,
not provider brand. They are advisory selection inputs and MUST NOT hide
authentication or request-validation errors.

Fallback re-runs capability and history-affinity checks. It receives a new
request ID and attempt record while preserving causal run/turn identity.

## Settlement

Cancellation is not settled until all run-owned tasks are terminal, safely
detached under a documented owner, or durably handed off. Required store and
audit writes use a bounded independent settlement token so cancelling the run
does not erase evidence of cancellation.

## Acceptance scenarios

- Caller cancellation, tool timeout, and budget stop yield different outcomes.
- Cancellation at every await boundary produces one terminal run event.
- A late tool result cannot overwrite an interrupted terminal result.
- Provider retry stops immediately on cancellation and respects fake time.
- Partial streamed output is never transparently duplicated by retry.
- Fallback rejects incompatible provider-bound history.

## Upstream evidence

- Pi uses a run-owned abort controller and preserves aborted responses in
  [`agent.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent.ts).
- OpenCode's retry policy and interrupted tool cleanup are in
  [`processor.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/opencode/src/session/processor.ts).
- Pydantic AI's concurrent task draining and tool timeouts are documented in
  [function tools](https://ai.pydantic.dev/tools/).

## Related specifications

- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Observability and audit](observability-and-audit.md)
