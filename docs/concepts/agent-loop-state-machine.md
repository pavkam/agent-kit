# Agent loop state machine

**Status:** Normative  
**Depends on:** [Agent and run context](agent-definition-and-run-context.md),
[messages](message-and-content-model.md),
[input admission](input-admission-and-message-queues.md)

## Purpose

The loop coordinates one run. Replaceable services decide how to build context,
select a model, execute tools, enforce policy, persist state, and stop.

## States

```text
Created
  -> AdmittingInput
  -> PreparingTurn
  -> AwaitingModel <-> StreamingModel
  -> RecordingToolCalls
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

## Turn algorithm

The default loop MUST implement these semantic steps:

1. Admit caller input durably or into the configured queue before acknowledging
   acceptance.
2. Promote eligible input using the queue's atomic ordering rules.
3. Repair and validate the history boundary.
4. Resolve the next-turn effective configuration, tools, model, instructions,
   and budgets.
5. Build a bounded working context without mutating durable history.
6. Persist or publish a model-request-started boundary.
7. Stream one provider response into typed events and a candidate immutable
   assistant message.
8. Validate the terminal provider outcome before committing the message.
9. If there are accepted tool calls, record them before side effects, then run
   the permission and execution pipeline.
10. Commit exactly one terminal result per accepted call in deterministic source
    order.
11. At the tool-turn boundary, promote eligible steering input and decide
    whether another model request is required.
12. When otherwise idle, promote at most the queue policy's allowed follow-up
    work and continue, or complete the run.
13. Perform post-run compaction, retry, persistence, and observer drains, then
    settle.

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
- A tool result and its durable terminal event MUST be committed atomically
  where the store supports transactions, or idempotently recoverable otherwise.
- Promotion of queued input MUST be atomic with recording its promoted sequence.

## Acceptance scenarios

- A no-tool response transitions from model completion to settlement once.
- Multiple tool calls execute according to scheduling policy and are committed
  in source order.
- Input arriving after a steering cutoff waits until the next boundary.
- An interrupted stream never emits a successful assistant completion.
- Restoring after a crash does not re-admit input or lose a recorded tool call.
- A custom loop passes the same externally observable lifecycle suite.

## Upstream evidence

- Pi's nested turn/follow-up behavior is in
  [`agent-loop.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent-loop.ts).
- OpenCode V2 separates durable admission, promotion, model work, and
  continuation in
  [`input.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/input.ts)
  and
  [`llm.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/runner/llm.ts).
- Pydantic AI models inspectable graph nodes in
  [`_agent_graph.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_agent_graph.py).

## Related specifications

- [Run lifecycle and settlement](run-lifecycle-and-settlement.md)
- [Tool-call lifecycle](tool-call-lifecycle.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
