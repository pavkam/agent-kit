# Run lifecycle and settlement

**Status:** Normative

**Architecture:** [Agent runtime](../architecture/agent-runtime.md),
[Input and output](../architecture/input-and-output.md),
[Sessions](../architecture/sessions.md)

**Depends on:** [Agent loop](agent-loop-state-machine.md),
[streaming events](streaming-and-event-protocol.md)

## Four completion boundaries

AgentKit MUST distinguish:

| Boundary            | Meaning                                                                                        |
| ------------------- | ---------------------------------------------------------------------------------------------- |
| Generation complete | The provider emitted a validated terminal response.                                            |
| Turn complete       | Assistant output and all accepted tool results for the turn are committed.                     |
| Run complete        | No further turn or queued input will be promoted in this run.                                  |
| Run settled         | All run-owned persistence, retry, compaction, middleware, and event delivery work is finished. |

Public methods MUST document which boundary they await. `RunAsync` MUST await
the bounded settlement attempt for accepted work, or detach its caller on
invocation cancellation. A separate streaming handle MAY expose earlier
boundaries without claiming the run is idle. Recovery-required completion is not
evidence that the run reached the settled boundary.

## Execution-lane coordination

The default [session executor](sessions-persistence-and-branching.md) MUST
permit at most one active operation per execution lane. Calls for different
lanes MAY overlap provider and tool effects, including lanes in the same
session. Their durable read-decide-write transitions serialize through the
session mutation coordinator; concurrent effects do not imply concurrent
unfenced branch-tip writes.

A simpler host MAY configure one lane per session. That is a composition
profile, not a universal session invariant.

When a caller starts work against a busy lane, the configured API MUST follow
the [input-admission contract](input-admission-and-message-queues.md) and do one
of the following explicitly:

- join the existing run;
- admit input as steering or follow-up work and return a receipt;
- wait for the active run and then start; or
- reject with `LaneBusy`.

It MUST NOT start a second uncoordinated loop on that lane. `Wake` SHOULD
coalesce duplicate requests by lane and expected operation identity. A stale
wake or abort for operation A MUST NOT affect successor operation B.

A lane is idle only when no durable operation is installed **and** no
process-local drive still owns it. An open operation without a driver is
recoverable work, not idle. A `RunWhenIdle`-style maintenance operation owns the
serialized idle window through its callback so command admission cannot slip
between the idle check and the protected action.

## Event sequence

At minimum, the runtime emits semantic events for:

```text
OperationAccepted
RunStarted
  TurnStarted
    ModelRequestStarted
    AssistantMessageCommitted
    ToolCallRecorded*
    ToolOutcomeRecorded*
    ToolResultMaterialized*
  TurnCompleted
RunCompleted | RunCancelled | RunLimitReached | RunFailed
RunSettled
```

Events MAY be enriched and live deltas interleaved according to the
[streaming event grammar](streaming-and-event-protocol.md), but durable semantic
events MUST preserve causal order. `RunSettled` is always the final run
lifecycle event.

`ToolOutcomeRecorded` marks the authoritative `OutcomeReady` commit and MAY
follow effect-completion order. `ToolResultMaterialized` marks the bounded
history projection and `Completed` transition in assistant source order. A
durable retry wait or provider suspension emits a nonterminal `RunWaiting` or
`RunSuspended` event with its operation identity, reason, and wake condition; a
later drive emits `RunResumed`. None of those events imply run completion or
settlement.

## Observer settlement

Observers MUST NOT execute inside the loop's state mutation critical section.
The dispatcher MUST isolate observer failures and apply an explicit backpressure
policy.

Awaited critical sinks, such as the durable event store, are part of settlement.
Under the [observability contract](observability-and-audit.md), best-effort
telemetry exporters MAY drain independently only when dropping or delaying them
cannot change the run result. The distinction MUST be configured, not guessed by
sink type.

## Post-run work

Automatic retry, [context compaction](context-compaction.md), snapshot creation,
final metadata hooks, and queued input promotion can extend a run past a nominal
agent-end event. Implementations MUST either perform such work before
`RunCompleted` or represent the transition and return to active turns
explicitly. They MUST NOT emit `RunSettled` and later resume the same run.

An asynchronous before-end hook or continuation callback produces a proposal,
not a terminal decision. Before accepting its follow-up or committing the
terminal result, the runtime reacquires the session mutation line and rechecks
operation identity, state, queue cutoff, and planned input IDs. Newly admitted
external work takes precedence over a stale internally generated follow-up.

A durable retry delay or deferred provider handle remains an open operation. A
driver may return a typed `Waiting` result containing the next attempt,
not-before time, owner, and wake conditions without claiming completion or
installing a hidden process timer.

## Cancellation and stop

[Durable cancellation requests](cancellation-timeouts-and-resilience.md)
transition the expected operation into `Cancelling`; they do not rewrite prior
terminal tool results. Cancelling one caller's wait, stream, or RPC invocation
MUST NOT silently become a durable abort request. Settlement MUST await or
safely abandon run-owned tasks according to the tool cancellation contract.

An application-requested graceful stop SHOULD finish the current atomic
boundary, commit a truthful partial outcome, skip new side effects, and settle.
An immediate cancellation may interrupt streaming but still MUST clean up and
emit one terminal lifecycle result.

A loop may let caller cancellation fault its invocation only while the run has
produced no durable effect. Once the run has committed any message (an
interrupted assistant message, an assistant tool request, or a tool-result
message settling an interrupted batch), cancellation MUST settle as a typed
cancelled outcome whose result reports every committed message and the exact
branch version. Throwing after a durable commit would discard a result the
caller needs in order to reason about the session's state.

Host close is neither cancellation nor settlement. Attachment may report an open
operation, and close may deliberately leave it recoverable after sealing new
process-local effects. It MUST NOT synthesize a terminal assistant message or
run result merely to make the process look tidy.

## Result availability

The semantic outcome, output, message order, and terminal usage snapshot MUST
become immutable at the terminal run transition. The final
`AgentRunFinished<TOutput>` envelope is constructed after the bounded settlement
attempt and includes a separate typed `RunSettlementOutcome`. It reports either
completed settlement or recovery required, preserving the semantic outcome in
both cases. Clean success requires both `RunSucceeded` and
`RunSettlementCompleted`.

If required persistence or audit fails, the runtime returns recovery required
when it can report that truthfully; it MUST NOT emit a successful `RunSettled`
event or claim that a lost append committed. The durable operation retains its
pending settlement state. A later recovery appends correlated reconciliation
facts and may finish settlement exactly once; it does not mutate the previously
returned envelope. Pre-admission rejection is a separate `AgentRunRejected`
result and MUST NOT fabricate a run identity.

Required delivery of preceding events MUST reach its configured boundary before
`RunSettled` commits. For those events, the sink policy selects durable outbox
acceptance or external acknowledgement. The final marker itself settles at
atomic durable acceptance of `RunSettled` and its publication outbox intent; its
own external acknowledgement MUST NOT become another run-settlement barrier.
Delivery acknowledges that intent idempotently without appending another
run-lifecycle event. A consumer needing proof of final-marker delivery awaits
the separate delivery receipt. Best-effort telemetry never participates in this
barrier.

`IOutputPublisher.CompleteAsync` exposes the already constructed final envelope
to subscribers after this attempt. It MUST NOT introduce a new required effect
that retroactively determines the envelope's settlement status. Any required
final-result persistence or publication intent is prepared by the settlement
protocol before that envelope becomes observable.

## Acceptance scenarios

- A slow awaited observer delays settlement but not message commit ordering.
- An observer exception is recorded and cannot corrupt run state.
- Two simultaneous wake calls produce at most one successor drain.
- Different lanes in one session may overlap effects while durable commits stay
  serialized and branch-correct.
- New work for a stopping lane begins after the old operation settles.
- A stale wake for an earlier operation cannot advance its successor.
- An installed operation with no driver is not reported as idle.
- New external input admitted during a before-end hook wins revalidation over
  the hook's stale follow-up proposal.
- Closing and reattaching preserves an open operation without fabricating a
  terminal result.
- `RunSettled` is the final run-lifecycle event; later reconciliation facts name
  the settled run only as causal evidence and do not reopen its lifecycle.
- Settlement failure preserves semantic output and returns recovery required.
- A final envelope rejects mismatched cursor, usage, message or deferred-request
  correlation before publication; interrupted committed content retains its
  explicit state.
- Cancelling or disposing a stream releases its delivery buffer while its
  repeatedly awaitable completion still returns the identical final envelope.
- Retrying terminal publication uses the same outbox/event identity.
- Failure of a required store append prevents a clean-success result.

## Related specifications

- [Streaming and event protocol](streaming-and-event-protocol.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Observability and audit](observability-and-audit.md)
