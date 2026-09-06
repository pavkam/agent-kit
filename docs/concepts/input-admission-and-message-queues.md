# Input admission and message queues

**Status:** Normative  
**Depends on:** [Run lifecycle](run-lifecycle-and-settlement.md),
[sessions](sessions-persistence-and-branching.md)

## Purpose

Input can arrive while an agent is streaming, executing tools, idle, or
recovering. AgentKit separates durable acceptance from promotion into model
context so no message is lost, duplicated, or injected at an unsafe boundary.

The first-party implementation belongs to
[`AgentKit.IO`](../architecture/input-and-output.md). It coordinates admission
and promotion through the
[`AgentKit.Session` contracts](sessions-persistence-and-branching.md) so queue
state and conversation state cannot diverge into separate sources of truth.

## Input record

The canonical `AgentInput`, `AdmittedInput`, `InputDelivery`, and admission
outcome shapes are defined once in the
[input architecture](../architecture/input-and-output.md#normative-minimal-input-contracts).
An admitted record MUST preserve its typed caller input and durable admission
identities, addressed agent and session, monotonic admitted sequence, delivery
class, immutable payload, complete immutable `ExecutionIdentity`, admission
timestamp, and optional promotion sequence. Tenant and principal projections do
not replace the identity's evidence, assurance, delegation chain, or version.

`Steer` means deliver at the next safe turn boundary. `FollowUp` means deliver
only when current work would otherwise finish. Names MAY differ, but the two
semantics MUST remain distinct.

## Idempotent admission

Admission MUST occur before promotion and before an API acknowledges success. It
MUST be idempotent by caller- or runtime-supplied `InputId`:

- the same ID and equivalent immutable payload returns the existing receipt;
- the same ID with a different payload returns `InputConflict`; and
- retries MUST NOT create a second admitted sequence.

Input size and schema validation MUST happen before observable admission.
Authorization to address the session MUST also happen before append. The
admitted identity MUST equal the identity captured in its
`SecurityAuthorizationContext`; any mismatch fails before session lookup.

## Atomic promotion

Promotion marks an input visible to the loop. It MUST atomically record a
promotion event sequence or use an idempotent transaction that is equivalent.
Promotion reuses the durably admitted identity snapshot and MUST NOT substitute
an ambient, reauthenticated, or current host principal. A host that requires a
new identity starts a new authorized admission or explicit reauthentication
boundary.

At each steering boundary the executor MUST capture a durable event-sequence
cutoff, then promote all unpromoted steering inputs admitted at or before that
cutoff in admitted order. Inputs arriving after the cutoff wait for the next
boundary.

When otherwise idle, it MUST promote exactly one oldest follow-up input by
default, then include steering inputs eligible at that promotion's cutoff.
Promoting one follow-up prevents a busy producer from consuming an unbounded
queue in a single run. An alternative batch policy MAY be configured but MUST
remain bounded and deterministic.

## Safe boundaries

At the [loop's named safe boundaries](agent-loop-state-machine.md), steering
input MAY be promoted:

- before the first model request;
- after an assistant message and all accepted tool results for that turn are
  committed; or
- after a continuation checkpoint explicitly defined by the loop.

It MUST NOT be inserted between a tool call and its result, into the middle of
an assistant content block, or retroactively into an in-flight request.

Non-triggering application annotations MAY be buffered and committed at a turn
boundary. Their delivery policy MUST say whether they cause another model
request.

## Queue contract

A durable `IInputQueue` MUST define:

- ownership and isolation key;
- monotonic ordering and pagination;
- atomic admission and promotion operations;
- lease/visibility semantics when consumers may fail;
- idempotency and optimistic concurrency tokens;
- capacity, payload, age, and per-principal limits;
- cancellation and poison-input behavior; and
- retention and deletion behavior.

An in-memory implementation MAY omit leases but MUST preserve identical
ordering, idempotency, and cutoff semantics.

## Backpressure

Queue capacity is a [usage limit](usage-limits-and-budgets.md), not an
allocation accident. When full, admission MUST return a typed
`QueueCapacityExceeded` result with retry guidance. It MUST NOT silently drop
oldest input or block indefinitely.

Per-session and global limits SHOULD prevent one producer from starving others.
Metrics MUST expose admitted age, depth, promotion lag, conflicts, rejections,
and redelivery.

## Enqueued message correlation

When pending messages are delivered into a run, a typed event MUST list their
input IDs. UI text is not a durable queue key. Adjacent compatible user content
MAY be coalesced into one provider-facing request part, while all source IDs and
order remain traceable.

## Acceptance scenarios

- Retrying identical admission returns one record and one sequence.
- A conflicting duplicate fails without replacing the original.
- A steer arriving after the captured cutoff is not promoted early.
- One idle boundary promotes one follow-up and all earlier eligible steers.
- A crash after promotion can replay without redelivery into history.
- Queue-full behavior is immediate, typed, and observable.

## Upstream evidence

- OpenCode V2's admission, idempotency, cutoff, and promotion rules are in
  [`session/input.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/input.ts).
- Pi distinguishes steering and follow-up delivery with configurable drain modes
  in
  [`agent.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/agent/src/agent.ts).
- Pydantic AI models ASAP and when-idle priorities and correlates deliveries by
  enqueue ID in
  [`_enqueue.py`](https://github.com/pydantic/pydantic-ai/blob/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/_enqueue.py).

## Related specifications

- [Message and content model](message-and-content-model.md)
- [Agent loop state machine](agent-loop-state-machine.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
