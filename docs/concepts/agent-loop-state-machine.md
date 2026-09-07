# Agent loop state machine

**Status:** Normative

**Architecture:** [Agent runtime](../architecture/agent-runtime.md)

**Depends on:** [Agent and run context](agent-definition-and-run-context.md),
[messages](message-and-content-model.md),
[input admission](input-admission-and-message-queues.md)

## Purpose

The loop coordinates one run inside a durably accepted execution-lane operation.
Replaceable services decide how to build context, select a model, execute tools,
enforce policy, persist state, and stop. Acceptance, host scheduling, and
process-local drive ownership are outside the loop.

## States

```text
Accepted -> Driving -> PromotingInput -> PreparingTurn
PreparingTurn -> AwaitingModel <-> StreamingModel
StreamingModel -> RecordingToolCalls
Any retryable active state -> WaitingRetry -> Driving
StreamingModel -> SuspendedDeferred -> Driving
RecordingToolCalls
  -> AwaitingTools
  -> CommittingToolResults
  -> PreparingTurn ...
  -> Completing
  -> Settling
  -> Settled

Any active state -> Cancelling -> Settling
Any active state -> Failing -> Settling
```

`Settled` is terminal. A run MUST reach exactly one terminal outcome and emit
exactly one settlement event.

`Accepted` is already durable and may have no process-local driver. Every drive
names the expected operation identity; a stale wake cannot advance a successor.

`WaitingRetry` and `SuspendedDeferred` are durable, nonterminal operation
states. A drive pass MAY return a typed `Waiting` outcome and relinquish its
process-local ownership while the durable operation remains installed. Only an
explicit wake, poll, or later drive advances it; entering either state emits no
run-completion or settlement event.

## Turn algorithm

The default loop MUST implement these semantic steps:

1. Verify the already accepted durable operation and acquire its fenced drive
   ownership. Input admission and acknowledgement occurred before the loop was
   dispatched.
2. Ask the I/O coordinator to promote eligible input using the queue's atomic
   ordering rules.
3. [Repair and validate the history boundary](history-validation-and-repair.md).
4. Resolve the next-turn effective configuration, tools, model, instructions,
   and budgets.
5. Build a [bounded working context](context-assembly-and-instructions.md)
   without mutating durable history.
6. Prepare the exact provider request, reserve its response and usage
   identities, and durably record effect intent before provider I/O.
7. Stream one provider response into
   [typed events](streaming-and-event-protocol.md) and a candidate immutable
   assistant message.
8. Validate the terminal provider outcome before committing the message.
9. If there are accepted tool calls, record the batch and each call's canonical
   arguments, replay policy, source ordinal, and result identity before side
   effects, then run the [tool-call lifecycle](tool-call-lifecycle.md).
10. Stage exactly one authoritative terminal tool outcome per accepted call in
    completion order, then materialize its bounded history projection in
    deterministic source order.
11. At the tool-turn boundary, promote eligible steering input and decide
    whether another model request is required.
12. When otherwise idle, promote at most the queue policy's allowed follow-up
    work and continue, or complete the run.
13. Perform post-run compaction, retry, persistence, and observer drains, then
    [settle](run-lifecycle-and-settlement.md).

## Continuation conditions

Another model request is required when at least one of these holds:

- committed tool results require interpretation;
- steering input was promoted;
- an output validator requested a retry;
- context compaction completed and the interrupted request is retryable;
- a deferred operation was resolved; or
- an explicit loop policy requests continuation within remaining budgets.

The loop MUST NOT infer continuation merely because a provider used an unusual
raw finish reason. Provider adapters normalize terminal semantics first.

## Stop conditions

The loop terminates with a typed outcome for successful output, idle completion,
caller stop policy, cancellation, deadline, budget limit, policy halt,
unsupported capability, invalid state, or failure.

A turn limit SHOULD allow the final model request to receive a concise “final
response now” instruction with tools disabled when configured. It MUST not
silently pretend the agent chose to finish.

## Replacement contract

`IAgentLoop` MUST be replaceable. It consumes explicit collaborators and MUST
NOT resolve hidden dependencies from a container. A custom loop receives the
same provider-neutral contracts and MUST emit the same lifecycle and terminal
semantics to pass conformance.

## Atomicity rules

- Model output MUST NOT be committed as complete before the terminal stream
  event validates.
- Tool calls MUST be committed before invocation begins.
- Provider and tool intents MUST reserve stable result/usage identities before
  entering their uncertain effect window.
- A complete authoritative tool outcome, its terminal event, and the transition
  to `OutcomeReady` MUST be committed atomically where the store supports
  transactions, or be idempotently recoverable otherwise. The later bounded
  history projection and transition to `Completed` occur in source order and
  MUST never repeat the invocation.
- Current durable operation state MUST be total after every transition; recovery
  MUST NOT infer a phase from missing auxiliary records.
- Every drive dispatch MUST commit a changed total state, return a typed wait or
  terminal outcome, or fault. A successful `continue` without durable progress
  is an invariant violation.
- Promotion of queued input MUST be atomic with recording its promoted sequence.

## Acceptance scenarios

- A no-tool response transitions from model completion to settlement once.
- Multiple tool calls execute according to scheduling policy and are committed
  in source order.
- Input arriving after a steering cutoff waits until the next boundary.
- An interrupted stream never emits a successful assistant completion.
- Restoring after a crash does not re-admit input or lose a recorded tool call.
- Acceptance survives process loss before the first driver or effect starts.
- A completed later parallel tool does not replay while waiting for an earlier
  source-position result to materialize.
- Every persisted operation-state leaf has a tested recovery dispatch and none
  can hot-loop without a durable transition.
- A custom loop passes the same externally observable lifecycle suite.

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
