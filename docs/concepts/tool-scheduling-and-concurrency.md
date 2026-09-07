# Tool scheduling and concurrency

**Status:** Normative  
**Depends on:** [Tool-call lifecycle](tool-call-lifecycle.md),
[usage limits](usage-limits-and-budgets.md)

## Purpose

Parallel tool calls reduce latency and multiply ambiguity. AgentKit allows
concurrency only with deterministic publication, explicit barriers, and
preflighted limits.

## Source order

The [provider response](streaming-and-event-protocol.md) defines source order.
Calls MUST retain a stable part identity or position in the complete assistant
content sequence and receive a separate scheduling ordinal before execution.
Text and reasoning parts count toward content position. Scheduling MAY be
concurrent, but committed result messages and the next provider context MUST use
call source order unless an explicit tool protocol requires another
deterministic order.

Live completion events MAY arrive in completion order. They MUST carry call ID
and source ordinal so consumers do not mistake completion order for history
order.

## Scheduling modes

Each resolved tool may declare:

- `ParallelSafe`: can overlap with compatible calls;
- `Sequential`: executes alone as an ordering barrier;
- `ConcurrencyKey`: may overlap except with the same key;
- `GlobalExclusive`: executes alone in the configured scheduler scope; or
- `HostScheduled`: delegated to a scheduler with equivalent guarantees.

Host policy may tighten but never loosen untrusted tool hints.

## Barrier-segment algorithm

The default scheduler SHOULD partition source-ordered calls into segments:

```text
[parallel-safe calls] -> [sequential call] -> [parallel-safe calls] -> ...
```

Calls within a parallel segment start under the concurrency limiter. A
sequential call waits for the prior segment, runs alone, and completes before
the next segment. This preserves useful concurrency without making one
sequential tool serialize unrelated calls on both sides more than necessary.

Concurrency-key conflicts create additional deterministic sub-barriers. The
scheduler MUST avoid deadlock by sorting multiple resource keys canonically or
rejecting multi-key declarations.

## Preflight

Before starting any call in a provider-emitted batch, the runtime MUST perform
all side-effect-free work needed to decide whether the batch is schedulable:

- resolve and validate every call;
- evaluate call-count and concurrency limits;
- detect duplicate call IDs and tool-name ambiguity;
- establish source ordinals and barrier segments; and
- run [security prechecks](permissions-approvals-and-trust.md) that do not
  require interactive approval.

If executing the batch would exceed a hard successful-tool or call limit, none
of the batch starts. This prevents order-dependent partial side effects.

Calls denied or awaiting approval may be represented within the schedule, but
policy MUST state whether independent allowed calls proceed.

## Publication

Tool tasks MAY finish in any order. Each finished effect first commits one
complete bounded authoritative outcome and marks the call `OutcomeReady` in
completion order. That durable state prevents replay after a crash while an
earlier call is still running. A separate history-materialization coordinator
buffers only the bounded result projections until every earlier source ordinal
is terminal, then places those projections and marks calls `Completed` in source
order. Bounded result-size and call-count limits prevent an early slow call from
allowing unbounded staged output behind it.

If a call never settles by deadline, it receives an authoritative terminal
timeout or interrupted outcome so later projections can materialize.

## Cancellation

On [run cancellation](cancellation-timeouts-and-resilience.md) the scheduler
MUST stop admitting new segments, signal all running invocations, await them
within a bounded drain deadline, then mark unsettled calls interrupted.
Synchronous or remote side effects may continue; their result MUST say outcome
unknown rather than claim rollback.

Cancellation of one independent tool SHOULD NOT cancel siblings unless the batch
policy is fail-fast. A sequential barrier failure policy MUST state whether
later segments are skipped or proceed.

## Early output termination

If a [structured-output tool](structured-output.md) produces a final result, end
strategy decides which function tools still run. The scheduler MUST apply that
decision before starting avoidable side effects. Results already running follow
the cancellation and settlement policy.

## Acceptance scenarios

- Parallel calls finish in reverse order but commit in source order.
- Mixed text, reasoning, and tool parts retain distinct content position and
  scheduling ordinal.
- A sequential call forms a barrier between two parallel segments.
- A hard batch limit prevents every side effect, not just the last call.
- Duplicate call IDs fail preflight.
- Cancellation marks an uncooperative tool outcome unknown after drain timeout.
- Buffer bounds apply while an earlier ordinal is slow.
- A crash after later calls stage but before the head call settles replays only
  the head according to its effect classification.

## Related specifications

- [Structured output](structured-output.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Coding harness execution profile](coding-harness-execution-profile.md)
