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

- caller/invocation cancellation;
- durable operation abort;
- host shutdown;
- run deadline;
- operation timeout;
- policy stop;
- budget limit;
- superseded/interrupt request; and
- dependency failure cancellation.

An internal cancellation token MAY carry all of these, but the terminal result
and events MUST retain the normalized reason.

Invocation cancellation stops one caller's wait, stream, or RPC request. It MUST
NOT silently write durable abort or cancel work owned by another joiner. Durable
abort names the expected operation identity and is admitted through the session
mutation boundary. A stale abort for operation A cannot affect successor B.

Durable abort synchronously changes the process-local effect gate from `open` to
`aborting`, then commits the cancel marker and current-run queue pruning, then
resolves the gate barrier, and only afterward signals already admitted effects.
New effects cannot slip into the commit wait, and an in-flight effect cannot see
cancellation before its durable cause exists.

## Propagation

Every public asynchronous API accepts `CancellationToken`. The runtime derives
separate invocation and operation-owned signals, then passes the appropriate one
through every await, stream, provider attempt, tool, store, queue, middleware,
and observer operation. A shared receiver never retains a caller token as its
ambient current cancellation source.

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

On cancellation the scheduler follows the
[tool-result and retry contract](tool-errors-retries-and-results.md): it stops
starting calls, signals running calls, and waits a bounded drain period.
Asynchronous cooperative tasks SHOULD settle. Synchronous, process, network, or
remote tasks may continue despite local cancellation; after the drain deadline
they are marked interrupted with unknown effect unless an external
idempotency/status API proves otherwise.

Results arriving after terminal interruption are ignored for model context and
recorded as late diagnostics or reconciliation input. They MUST NOT create a
second terminal tool result.

Restored cancellation never replays a tool, even when its ordinary recovery
classification is safe. Planned calls receive synthetic aborted results;
`effect_pending` calls receive explicit interrupted results; already staged
outcomes remain real and materialize in source order.

## Provider retry

A provider resilience policy above the
[single-attempt adapter](provider-request-pipeline.md) classifies normalized
errors and decides retry, fallback, or fail. Retry MUST consider:

- whether any output became visible or was committed;
- provider idempotency and request status;
- retry hint and rate-limit reset;
- remaining requests, tokens, cost, and deadline;
- provider/model compatibility with current history; and
- configured maximum attempts.

Backoff is bounded exponential delay with injectable jitter by default. It MUST
honor cancellation. Authentication, authorization, invalid request, unsupported
capability, and protocol violations are not transient by default.

Context-overflow compaction/retry has a separate attempt budget from transport,
rate-limit, and dependency retry. A failed assistant attempt remains durable
audit evidence but does not re-enter the repaired request context. Compaction
and branch-summary provider calls record their own usage and retry state.

Provider retry hints are advisory evidence. HTTP-style adapters MUST support
both delay and absolute-date forms, normalize them at response-observation time
with the injected `TimeProvider`, and preserve safe provenance. Past dates clamp
to a zero minimum; malformed values are diagnosed and ignored. The retry owner
then applies configured maximum-delay, remaining-deadline, budget, attempt,
idempotency, visible-output, and side-effect rules. A hint never authorizes a
retry or bypasses those checks, and no component sleeps against an ambient
wall-clock value.

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
- Delta and absolute-date retry hints normalize deterministically under fake
  time and cannot extend work beyond the operation deadline.
- Cancelling one joined observer leaves the durably accepted operation active.
- A stale durable abort cannot cancel a later operation on the same lane.
- No effect starts after the local abort gate closes, and no admitted effect
  sees cancellation before the durable marker commits.
- Exhausting overflow-repair attempts does not consume or reset the unrelated
  transport-retry budget.

## Related specifications

- [Tool errors, retries, and results](tool-errors-retries-and-results.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Observability and audit](observability-and-audit.md)
- [Coding harness execution profile](coding-harness-execution-profile.md)
