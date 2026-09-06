# Run lifecycle and settlement

**Status:** Normative  
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

Public methods MUST document which boundary they await. `RunAsync` SHOULD await
settlement. A separate streaming handle MAY expose earlier boundaries without
claiming the run is idle.

## Single-active-run rule

The default [session executor](sessions-persistence-and-branching.md) MUST
permit at most one active mutating run per session. Calls for different sessions
MAY run concurrently.

When a caller starts work against a busy session, the configured API MUST follow
the [input-admission contract](input-admission-and-message-queues.md) and do one
of the following explicitly:

- join the existing run;
- admit input as steering or follow-up work and return a receipt;
- wait for the active run and then start; or
- reject with `SessionBusy`.

It MUST NOT start a second uncoordinated loop. `Wake` SHOULD coalesce multiple
wake requests into one successor drain.

## Event sequence

At minimum, the runtime emits semantic events for:

```text
RunStarted
  TurnStarted
    ModelRequestStarted
    AssistantMessageCommitted
    ToolCallRecorded*
    ToolResultCommitted*
  TurnCompleted
RunCompleted | RunCancelled | RunLimitReached | RunFailed
RunSettled
```

Events MAY be enriched and live deltas interleaved according to the
[streaming event grammar](streaming-and-event-protocol.md), but durable semantic
events MUST preserve causal order. `RunSettled` is always the final run
lifecycle event.

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

## Cancellation and stop

[Cancellation requests](cancellation-timeouts-and-resilience.md) transition the
run into `Cancelling`; they do not rewrite prior terminal tool results.
Settlement MUST await or safely abandon run-owned tasks according to the tool
cancellation contract.

An application-requested graceful stop SHOULD finish the current atomic
boundary, commit a truthful partial outcome, skip new side effects, and settle.
An immediate cancellation may interrupt streaming but still MUST clean up and
emit one terminal lifecycle result.

## Result availability

The final result MUST become immutable at `RunCompleted`, `RunCancelled`,
`RunLimitReached`, or `RunFailed`. Settlement may append operational metadata
such as persistence acknowledgements, but MUST NOT change semantic output or
message order.

If a critical settlement operation fails, the terminal result MUST indicate that
committed state may require recovery. Returning success while losing the session
append is forbidden.

## Acceptance scenarios

- A slow awaited observer delays settlement but not message commit ordering.
- An observer exception is recorded and cannot corrupt run state.
- Two simultaneous wake calls produce at most one successor drain.
- New work for a stopping session begins after the old run settles.
- `RunSettled` is emitted once and no later event uses that run ID.
- Failure of a required store append prevents a clean-success result.

## Upstream evidence

- Pi's agent listeners are awaited and its high-level session adds a stronger
  settled event in
  [`agent-session.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core/agent-session.ts).
- OpenCode's per-session run joining and wake coalescing are in
  [`run-coordinator.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/run-coordinator.ts).

## Related specifications

- [Streaming and event protocol](streaming-and-event-protocol.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Observability and audit](observability-and-audit.md)
