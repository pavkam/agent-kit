# Agent loop state machine

**Status:** Normative

**Architecture:** [Agent runtime](../architecture/agent-runtime.md)

**Depends on:** [Agent and run context](agent-definition-and-run-context.md),
[messages](message-and-content-model.md),
[input admission](input-admission-and-message-queues.md)

## Purpose

The loop coordinates one run inside an execution-lane operation committed to the
selected session store. Replaceable services decide how to build context, select
a model, execute tools, enforce policy, persist state, and stop. Acceptance,
host scheduling, and process-local drive ownership are outside the loop.

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

`Accepted` is already committed and may have no process-local driver. Its
process-loss guarantees are those of the selected session profile and store; an
explicitly ephemeral profile does not promise durable recovery. A profile that
promises durable admission must commit to a durable store before
acknowledgement. Every drive names the expected operation identity; a stale wake
cannot advance a successor.

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

### Continuation evaluation boundary

`IRunContinuationPolicy` receives an immutable `RunContinuationContext` at a
safe decision boundary. Its decision is a proposal until the session coordinator
accepts the corresponding state transition. The policy does not admit input,
reserve budget, authorize effects, repair output, or commit session records.

The context identifies the accepted operation, run, execution lane, relevant
operation-state revision, selected branch cursor, input-promotion cutoff, and
captured configuration and policy versions. It carries the typed observations
produced by the owning components and enough causal identity to revalidate them.
A session-wide append version may support a store compare-and-swap, but an
unrelated lane's append does not by itself invalidate a continuation decision.
Revalidation compares the state and inputs the decision actually depends on; a
failed store compare-and-swap retries that comparison.

The boundary distinguishes a committed turn from a retryable request or a
resolved deferred operation. A committed-turn boundary includes the complete
assistant response, committed tool-result evidence, and the output processor's
decision when output processing applies. A retry or deferred boundary may have
no completed assistant response. It preserves its existing request, turn, and
operation identities rather than inventing a completed turn. Constructors
validate the shape and correlation of these values; only the session owner can
establish that their referenced records actually committed.

Multiple continuation causes may coexist. `ContinuationReason` preserves the
selected cause and its evidence: committed tool results, promoted admission
identities, an output-repair decision, an activated compaction checkpoint with a
retryable request, a resolved deferred operation, or an explicit policy request.
Raw prompt text, provider finish strings, and the presence of an unpromoted
queue entry are not continuation evidence. Eligibility and promotion remain I/O
decisions. Budget and deadline observations are not reservations or grants.

The first-party policy applies these rules in order:

1. An operation already cancelling, failing, settling, or settled is handled by
   its state-machine path. It is not offered to continuation as ordinary work.
2. A required stop or exhausted hard limit prevents another request. The loop
   preserves the canonical typed stop cause; a policy cannot turn it into idle
   completion or widen the limit. If several stop requests compete, the session
   coordinator's committed ordering determines the primary cause and retains the
   other observations as evidence.
3. Eligible input already promoted at the boundary is considered before an
   internally proposed follow-up. Required interpretation of committed tool
   results, output repair, deferred completion, and retryable compaction then
   take precedence over optional policy continuation. Choosing one reason does
   not discard other pending causes from the next request's context.
4. Without a continuation cause, accepted output permits successful completion.
   Idle completion requires that no output decision or promoted work remains
   pending. Missing required output validation is invalid state, never success.

`CompleteRun` proposes only successful or idle completion supported by those
observations. `HaltRun` proposes the supported non-success terminal outcome.
Neither decision commits an outcome, publishes output, or proves settlement.
Unsupported or internally inconsistent decisions fail closed as invalid state.

After an asynchronous policy or hook returns, the loop reacquires the session
mutation boundary and rechecks operation identity and state, relevant revision,
branch cursor, input cutoff, planned admission identities, and any changed stop
condition. A stale proposal is discarded and reevaluated from a fresh snapshot;
it is never applied to a successor operation. Accepting the decision and
recording its next state are one coordinated transition. Each re-evaluation must
observe changed evidence, yield to its documented owner, or reach a typed
failure; a stale proposal cannot cause a busy loop.

The policy receives cancellation for its operation-owned evaluation, not a
detached caller's wait. Cancellation discards an unaccepted decision and returns
control to the operation's cancellation or recovery path. The first-party policy
reads no ambient clock or mutable queue state: time observations come from the
injected `TimeProvider` before evaluation, and repeated evaluation of the same
immutable context produces the same decision.

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
- A durable admission profile preserves acceptance across process loss before
  the first driver or effect starts; an ephemeral profile advertises no such
  guarantee.
- A completed later parallel tool does not replay while waiting for an earlier
  source-position result to materialize.
- Every persisted operation-state leaf has a tested recovery dispatch and none
  can hot-loop without a durable transition.
- A custom loop passes the same externally observable lifecycle suite.
- A compaction retry or deferred completion without a complete assistant
  response can reach its next valid request without fabricating turn evidence.
- Input admitted during asynchronous continuation invalidates an affected
  proposal; an unrelated lane's append alone does not.
- A continuation proposal cannot bypass a hard limit, required output
  validation, cancellation, or settlement.

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
