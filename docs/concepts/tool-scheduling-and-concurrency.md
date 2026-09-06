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
Calls MUST receive an ordinal before execution. Scheduling MAY be concurrent,
but committed result messages and the next provider context MUST use source
order unless an explicit tool protocol requires another deterministic order.

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

Tool tasks MAY finish in any order. A publication coordinator buffers terminal
results until all earlier source ordinals are terminal, then commits them in
order. Bounded result-size limits prevent an early slow call from allowing
unbounded buffered output behind it.

If a call never settles by deadline, it receives a terminal timeout or
interrupted result so later results can publish.

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
- A sequential call forms a barrier between two parallel segments.
- A hard batch limit prevents every side effect, not just the last call.
- Duplicate call IDs fail preflight.
- Cancellation marks an uncooperative tool outcome unknown after drain timeout.
- Buffer bounds apply while an earlier ordinal is slow.

## Upstream evidence

- Pydantic AI implements sequential tools as barriers between concurrent
  segments and preflights tool-call usage limits in its agent graph and tool
  manager; see
  [`_agent_graph.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_agent_graph.py).
- OpenCode V2 forks local tool execution while serializing publication in
  [`llm.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/runner/llm.ts).
- Pi supports parallel and sequential execution but serializes a whole batch
  containing a sequential tool in
  [`agent-loop.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent-loop.ts);
  AgentKit adopts the more precise barrier-segment rule.

## Related specifications

- [Structured output](structured-output.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
