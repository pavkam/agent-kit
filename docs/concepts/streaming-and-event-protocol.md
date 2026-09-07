# Streaming and event protocol

**Status:** Normative

**Architecture:** [Input and output](../architecture/input-and-output.md)

**Depends on:** [Messages](message-and-content-model.md)

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

An application profile MAY persist compact assistant-progress frames or
replaceable tool-progress snapshots. These are bounded recovery aids, not
semantic completion events. They never turn a live candidate into a committed
message or prove that an external effect ended.

## Run-event identity across recovery

The stable event identity is `(RunId, Sequence)`. The I/O publisher owns
allocation and, for recoverable runs, reserves sequence ranges durably before
publication. A new drive starts above the persisted high-water mark, not at one
or at the last durable semantic event. Lost live events and unused reservations
may leave gaps, which require an explicit loss/resnapshot boundary. Durable
redelivery preserves the originally committed event sequence. A provider's
contiguous per-attempt sequence is a different ordering domain.

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

## Wire projection and reduction

A frontend MAY project an internal snapshot-rich stream to a delta-only wire
grammar. That grammar MUST define one bounded base (`ResponseStarted` or an
equivalent message-start snapshot), ordered part deltas, and one authoritative
terminal aggregate. It states whether each usage update is cumulative or
incremental and preserves usage report state; missing usage never becomes zero.

Tool-call identity and name MUST be available no later than the first arguments
delta, either in the part-start frame or an earlier correlated descriptor. A
projection MUST NOT attach the complete response-so-far to every delta. For a
response of `n` content bytes, total encoded content is bounded linearly by `n`
plus declared per-frame overhead; repeated cumulative prefixes that produce
quadratic traffic violate the adapter contract.

The protocol publishes a deterministic reducer and conformance fixtures. A
client applying the base, every delta exactly once, and the terminal aggregate
must obtain the same terminal response as the canonical stream. Reconnect uses
an epoch/cursor-bound replacement base and never mixes deltas from an earlier
base. The terminal aggregate may supply later metadata or usage and therefore
outvotes provisional live state.

## Durable partials and reconnect

When partial durability is selected, one encoder consumes provider events in
order and writes at most one compact frame for each nonterminal event. It uses
part identity or `contentIndex`, supports interleaving, and avoids cloning the
ever-growing message for each token. Terminal response events are excluded;
final settlement is a separate atomic record.

Frame persistence SHOULD enqueue writes synchronously in event order without
awaiting storage for every delta. The implementation observes every write
failure and retains an ordered tail/fence whose completion proves all earlier
accepted writes finished before response settlement. This bounds provider
backpressure without permitting settlement to race queued persistence.

After process loss, reduction of the committed frame prefix yields a
**provisional** partial for display or an explicit interrupted result. It is not
provider-stream resumption. A snapshot keeps that partial outside the immutable
transcript until a complete or synthetic terminal message commits.

A reconnecting subscription registers a bounded event buffer and captures its
durable snapshot under the same mutation boundary. It then emits the snapshot,
events after the captured sequence, and live events. Replaying historical start
or delta events in addition to a snapshot is forbidden unless the protocol
explicitly de-duplicates them.

Registration occurs before snapshot capture and delivery remains buffered until
the consumer explicitly starts. A resnapshot uses a new epoch and sequence
barrier: discard delivery at or before the replacement boundary, hold later
events during capture, publish the replacement snapshot, then resume. Recipient
sets are fixed at publication time and payloads are immutable or cloned per
recipient. A failing subscriber is isolated and diagnosed without recursively
feeding the diagnostic through that same subscriber.

A failed replacement capture cannot silently resume the prior snapshot after the
new epoch has discarded any pre-boundary delivery. The implementation MUST
either retry atomically while retaining every event needed to close the gap, or
terminate the subscription with a typed resnapshot failure and release its
buffer. It MUST NOT combine the old snapshot with only the held post-boundary
events. This is an AgentKit safety requirement: a correct happy-path
epoch/barrier protocol is insufficient unless its fallback semantics also close
the delivery gap explicitly.

An RPC/event channel multiplexing commands, acknowledgements, semantic events,
and extension UI requests treats them as separate typed frame families. A
preflight or `Accepted` response is not operation completion. Frames declare
protocol version, required correlation, maximum encoded size, unknown-version
behavior, event sequence or replay cursor when supported, and backpressure.
Concurrent commands either serialize or carry expected-state/version evidence;
their responses and events may otherwise interleave.

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
- Delta-only projection scales linearly, exposes tool-call identity before
  argument streaming, and reduces to the same authoritative terminal message.
- A slow live UI cannot lose durable tool completion.
- Cancelling a subscriber leaves the run active when subscriptions are
  non-owning.
- Exactly one completion task result agrees with exactly one stream terminal.
- Process loss restores only the committed frame prefix and never promotes it to
  success.
- Snapshot plus buffered live events has no registration gap or duplicate.
- Resnapshot changes epoch without dropping a post-boundary event or replaying a
  pre-boundary event.
- Failed resnapshot capture either retries without a gap or terminates the
  subscription; it never resumes an old snapshot with a partial later stream.
- An accepted RPC prompt later emits exactly one independently correlated
  terminal outcome.

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Provider request pipeline](provider-request-pipeline.md)
- [Observability and audit](observability-and-audit.md)
