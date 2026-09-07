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

The coding-harness profile distinguishes four delivery classes:

- `Steer` delivers at the next safe boundary of the current run;
- `FollowUp` delivers only when current work would otherwise finish;
- `NextRun` is reserved for the next accepted run operation; and
- `Write` is non-triggering deferred tree content.

Names MAY differ, but these semantics remain distinct. A control command is not
a conversational queue item; its descriptor states whether it is legal during
generation, tool execution, retry delay, compaction, cancellation, and recovery.

## Resource-backed input and control commands

A slash command, skill invocation, or prompt-template invocation first resolves
against the captured
[coding-harness resource catalog](coding-harness-resources-and-project-trust.md#command-routing-and-prompt-expansion).
A recognized control command is routed as its own typed operation and never
falls through into `Steer` or `FollowUp`. An unrecognized command-like string
follows an explicit profile rule: reject it, or admit it as ordinary user text
with no implied authority.

For conversational input, admission preserves both the original caller payload
and the immutable transformed/expanded content plus its resource and hook
manifest. Idempotency compares the caller payload under the same `InputId` and
returns the already admitted expansion; a retry never reruns input hooks or
reads a newer skill/template version. Expansion, validation, and bounds failure
occur before admission and return typed outcomes. Queue snapshots and promotion
events expose the immutable admitted result, not a resource reference that may
resolve differently after reload.

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

Starting a run is one transaction, not a chain of hopeful acknowledgements. It
claims the expected lane, selects eligible items at a cutoff, promotes their
pending payloads to immutable entries, places the initiating prompt, deletes the
pending forms, advances the branch tip, and installs complete initial operation
state. Provider lookup and every external effect occur later.

At a proposed finish boundary, asynchronous hooks or policy callbacks run
outside the mutation line. Their follow-up is only a proposal. The executor
re-enters the serialized boundary and revalidates the lane state, cutoff, and
exact input-ID plan; newly admitted external work wins over a stale internally
generated follow-up.

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

Every queue snapshot, event, retraction, and editor restoration retains the
typed item ID, all content parts and attachments, source, delivery class,
admitted sequence, admission status, and immutable identity. Retraction is a
state transition on that identity, never removal by matching display text.
Duplicate text and image-distinct inputs are therefore unambiguous.

Durable abort removes only current-run `Steer` and `FollowUp` items selected by
its committed cutoff. It preserves `NextRun`, `Write`, and input admitted after
the abort marker. The abort result identifies every removed item so a UI may
offer explicit restoration without fabricating a new admission.

## Deferred-write ordering

A profile that accepts non-triggering `Write` items MUST name every drain
boundary and preserve their admitted order relative to later direct appends. A
direct append MUST NOT jump ahead of older deferred writes or leave them
permanently stranded merely because the lane became idle.

The first-party coding-harness profile uses this policy: an append received
while an operation owns the lane becomes a deferred `Write`; a later append
against an idle lane atomically materializes all older `Write` items in FIFO
order before the new entry. Another profile MAY reject appends during an
operation or choose a different bounded drain boundary, but it MUST specify and
test ordering, starvation, cancellation, and crash recovery.

## Acceptance scenarios

- Retrying identical admission returns one record and one sequence.
- A conflicting duplicate fails without replacing the original.
- Retrying one resource-backed input after catalog reload returns its original
  expansion without rerunning hooks.
- A control command cannot enter a steering/follow-up queue through slash-text
  fallthrough.
- A steer arriving after the captured cutoff is not promoted early.
- One idle boundary promotes one follow-up and all earlier eligible steers.
- A stale finish-hook proposal cannot jump ahead of newly admitted external
  input.
- Retracting one of two text-identical items preserves the other and every
  attachment.
- Aborting a run preserves next-run work and deferred writes.
- An idle direct append cannot overtake older deferred writes, and one atomic
  commit materializes the selected FIFO prefix plus the new entry.
- A crash after promotion can replay without redelivery into history.
- Queue-full behavior is immediate, typed, and observable.

## Related specifications

- [Message and content model](message-and-content-model.md)
- [Agent loop state machine](agent-loop-state-machine.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
