# Input and output

**Role:** Define how work enters an agent and how activity and results leave it.

I/O is a protocol boundary around the runtime. It accepts application input,
queues work safely, exposes live progress, and returns one typed final result.
Channel adapters such as HTTP, console, chat, UI, or background workers
translate their own protocols at this boundary; they do not become part of the
loop.

## Input admission

Input is authorized, bounded, validated, and durably admitted before the caller
receives success. Each admission has a stable identity and is idempotent. A
repeated equivalent request returns the existing receipt; the same identity with
different content is a conflict.

Two delivery classes are preserved. Steering input becomes eligible at the next
safe turn boundary. Follow-up input waits until the current work would otherwise
finish. Promotion uses a captured cutoff and deterministic order, so concurrent
arrivals are never inserted into an in-flight provider request or between a tool
call and its result.

The queue defines capacity, ordering, leases where needed, retention, poison
input behavior, and backpressure. Full queues fail with a typed outcome rather
than silently dropping data or blocking forever.

## Live output

Live output is an ordered stream of typed events: response and part boundaries,
content deltas, tool progress, usage updates, lifecycle transitions, and
diagnostics. Deltas are provisional. Consumers cannot mutate the candidate
message, and abandoning a subscription does not cancel the run unless the API
explicitly grants subscription ownership.

Fan-out is bounded. Required consumers may apply backpressure; best-effort
consumers may receive coalesced deltas, a dropped-event marker, or
disconnection. Durable semantic events are never silently dropped.

## Final output

The final result identifies the run and session, terminal outcome, newly
committed messages, usage, deferred requests, and validated application output.
Success, idle completion, deferral, cancellation, policy halt, limit exhaustion,
and failure are distinct outcomes.

Structured output is locally validated even when a provider claims native schema
enforcement. Text, schema-backed data, synthetic output tools, provider- native
output, media, and named union alternatives remain distinct modes. Validation
retries use their own budget. Provisional streamed data never becomes final
application output before terminal validation.

## Boundary rules

Input adapters cannot append arbitrary history or bypass session authorization.
Output adapters cannot infer state from display text or treat live deltas as
durable facts. The runtime remains usable without any specific UI, transport, or
hosting model.

## Related concept specifications

- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
- [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)
- [Structured output](../concepts/structured-output.md)
