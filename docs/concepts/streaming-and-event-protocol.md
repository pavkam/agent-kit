# Streaming and event protocol

**Status:** Normative  
**Depends on:** [Messages](message-and-content-model.md),
[run lifecycle](run-lifecycle-and-settlement.md)

## Purpose

Streaming is a typed protocol with ordering and terminal guarantees. It is not
an `IAsyncEnumerable<string>` with tool JSON smuggled through callbacks.

## Event classes

AgentKit MUST separate:

- **durable semantic events**, sufficient to replay stable session and run
  state; and
- **live events**, useful while work is active but not required for replay.

[Durable semantic events](sessions-persistence-and-branching.md) include
admission, promotion, message start/commit, tool-call recording, bounded tool
progress checkpoints, terminal tool result, compaction, configuration/model
changes, and lifecycle terminals.

Live events include text/reasoning deltas, fragmented tool arguments, raw
provider heartbeat/progress, and high-frequency output chunks. An application
MAY persist them separately for diagnostics, but the core event log MUST NOT
require them to recover stable state.

## Stream grammar

One assistant response follows this grammar:

```text
ResponseStarted
  (PartStarted | PartDelta | PartCompleted | UsageUpdated)*
ResponseCompleted | ResponseFailed | ResponseCancelled
```

A part MUST start before its own deltas or completion, but several parts MAY be
open and their events MAY interleave. A part index or stable part ID MUST
correlate every fragment. Successful completion requires every started part to
have completed; failure or cancellation records bounded partial parts as
interrupted. Exactly one response terminal MUST occur. No response event may
follow its terminal.

Tool argument fragments MAY interleave across call IDs when the provider does;
the adapter parser MUST assemble them independently. A terminal tool-call part
is not accepted until its arguments are bounded and syntactically complete.

## Candidate versus committed state

Live response events describe a candidate message under construction. Under the
[message commitment rules](message-and-content-model.md), the runtime MUST
publish an immutable committed assistant message only after:

- the provider terminal event validates;
- all open parts are resolved or marked interrupted;
- normalized stop reason and usage are finalized; and
- history invariants pass.

Live events MUST NOT expose a writable shared partial-message object. Consumers
receive immutable snapshots or typed deltas.

## Consumer behavior

The canonical generic streaming handle is defined once in the
[output architecture](../architecture/input-and-output.md#normative-minimal-output-contracts).

Cancelling enumeration MUST NOT implicitly cancel the run unless the API says
so. Consumer abandonment MUST dispose its subscription and release buffers. Run
cancellation is an explicit operation or token.

## Backpressure and fan-out

The dispatcher MUST use a bounded policy. Supported choices MAY include:

- backpressure the producer for a required sink;
- bounded coalescing for text/reasoning deltas;
- drop live-only events with a dropped-count marker; or
- disconnect a slow best-effort subscriber.

Durable semantic events MUST NOT be silently dropped. Observer failure MUST be
isolated from loop state and reported through diagnostics.

## Provider parser requirements

The [provider request pipeline](provider-request-pipeline.md) requires adapters
to treat streaming as a state machine and validate:

- transport framing and maximum frame size;
- response and item correlation;
- allowed event transitions;
- duplicate or missing terminal events;
- valid incremental encoding of text and JSON arguments;
- usage placement and accumulation; and
- transport end before protocol completion.

A malformed or truncated stream returns a protocol failure with safe partial
diagnostics; it MUST NOT synthesize success.

## Acceptance scenarios

- Every meaningful byte and event fragmentation boundary produces the same
  committed message.
- Two interleaved tool-call argument streams assemble under the correct IDs.
- Missing terminal, duplicate terminal, and delta-before-start each fail typed.
- A slow live UI cannot lose durable tool completion.
- Cancelling a subscriber leaves the run active when subscriptions are
  non-owning.
- Exactly one completion task result agrees with exactly one stream terminal.

## Upstream evidence

- Pi's start/content-delta/done stream event model is in
  [`packages/ai/src/types.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/ai/src/types.ts).
- OpenCode V2 explicitly separates replayable boundaries from live deltas in
  [`session-event.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/schema/src/session-event.ts).
- Pydantic AI exposes part start, delta, and end events in
  [`messages.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/messages.py).

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Provider request pipeline](provider-request-pipeline.md)
- [Observability and audit](observability-and-audit.md)
