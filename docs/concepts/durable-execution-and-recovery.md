# Durable execution and recovery

**Status:** Normative extension boundary

**Architecture:** [Durable execution](../architecture/durable-execution.md)

**Depends on:** [Sessions](sessions-persistence-and-branching.md),
[run lifecycle](run-lifecycle-and-settlement.md),
[tool-call lifecycle](tool-call-lifecycle.md)

## Purpose

Durable execution resumes useful work after process loss without replaying
unsafe side effects or serializing the runtime's object graph.

## Durability boundary

AgentKit core defines deterministic operations and durable records. A durable
backend adapter maps them to Temporal, DBOS, Restate, Prefect, or another
orchestrator. The core MUST NOT depend on a specific backend SDK.

Durability is a capability. In-memory execution MUST NOT imply crash recovery,
and a local per-session lock MUST NOT imply distributed ownership.

## Recoverable operation model

Each durable operation MUST have:

- stable operation and idempotency IDs;
- session/run/turn and causal parent identity;
- versioned serializable input and result;
- declared retry and timeout owner;
- cancellation semantics;
- side-effect and idempotency classification;
- deterministic operation name/version; and
- a checkpoint or terminal record.

Operation acceptance and execution ownership are separate. Acceptance commits
immutable metadata plus one complete initial state and starts no effect. Every
later transition replaces the complete current state or writes one terminal
result. Recovery dispatches from that total state; it does not fold a mutation
journal or infer progress from an absent auxiliary value.

[Model requests](provider-request-pipeline.md),
[tool calls](tool-call-lifecycle.md), compaction,
[external approval waits](deferred-and-human-in-the-loop.md), and selected hooks
MAY be durable operations. Pure context assembly normally remains a
deterministic replayable function unless expensive enough to checkpoint.

Attaching is side-effect free. It validates and reconstructs projections and
returns the inventory of open lane/operation identities, kinds, start times, and
abort state. It starts no provider, tool, hook, retry, poll, or timer; a caller
must explicitly drive or schedule each operation. Host close may therefore leave
a valid open operation for later attachment.

The serializable state union is a total dispatcher algebra. Every leaf has one
recovery procedure and cancellation meaning. A drive step must commit a changed
state, return a typed wait/terminal outcome, or fault. `Continue` without
durable progress is an invariant failure rather than a retry loop.

## Checkpoints

The runtime SHOULD checkpoint after these boundaries:

- input admission and promotion;
- context/configuration manifest creation;
- provider response terminal validation;
- tool-call recording before side effects;
- each complete authoritative tool outcome and its transition to `OutcomeReady`;
- source-ordered assistant/tool message materialization and the transition to
  `Completed`;
- compaction activation; and
- final run settlement.

Fine-grained token or progress deltas are not required for recovery. An
application MAY retain bounded assistant frames or complete tool-progress
snapshots for reconnection and truthful interruption output. They remain
auxiliary: apparent complete text, a successful shell line, or a
terminal-looking JSON fragment does not prove external settlement.

Every uncertain effect uses a durable sandwich: commit exact intent and reserved
result identities, invoke the effect, then atomically stage or commit the full
outcome with the next total state. Hook replay contracts are separate; a hook
whose result was not consumed durably may rerun.

## Recovery classification

After failure, every nonterminal operation MUST be classified:

| Evidence                                          | Recovery                                              |
| ------------------------------------------------- | ----------------------------------------------------- |
| Proven not started and prior owner fenced out     | Reauthorize, reserve, and start under policy          |
| Started with idempotency key and queryable result | Reconcile, then retry/query                           |
| Started, effect unknown, non-idempotent           | Do not retry; require operator/tool reconciliation    |
| Terminal result exists, commit missing            | Idempotently commit without reinvocation              |
| Complete outcome is durably staged out of order   | Materialize it when its source position is eligible   |
| Durable external owner accepted handoff           | Resume waiting/query that owner                       |
| Durable retry or deferred not-before state        | Return waiting; wake and re-drive after its condition |

“Probably failed” is not a recovery policy.

## Determinism

Replayable orchestration uses injected clocks, IDs, randomness, and versioned
configuration/catalog snapshots. It MUST NOT perform live DI discovery,
environment reads, network I/O, or mutable global access during deterministic
replay.

When code or schema versions change, an adapter MUST run an explicit migration,
pin the old operation version, or stop with `RecoveryIncompatible`. Silent
reinterpretation of recorded input is forbidden.

## Leases and fencing

Distributed execution requires one authoritative owner per claimed lane or
operation, plus serialized session mutations across lanes. Leases MUST have
expiration, renewal, owner ID, and a monotonically increasing fencing token. The
lease service is authoritative for expiry; worker wall clocks cannot extend
ownership. Every durable write verifies the current token.

A storage fence does not cancel an external effect. Takeover MUST require
receiver fencing, idempotency, or reconciliation before another invocation;
otherwise the operation remains unknown and requires action. Session, journal,
grant, and budget capabilities must cover the requested distributed recovery
domain. An in-memory grant ledger cannot support crash-safe durable grants.

Wake signals are hints and MAY coalesce.
[Durably admitted inputs](input-admission-and-message-queues.md) remain the
source of truth, so a lost or duplicate wake does not lose or duplicate work.

## Provider calls

Provider calls are not inherently replay-safe. An adapter MUST record request
identity before send and use provider idempotency/continuation facilities when
available. If a crash occurs after send without a recoverable response, policy
chooses reconcile, new explicitly duplicated request, or fail; it MUST not
pretend exactly-once delivery.

Persisted assistant frames are a committed prefix, not a resumable provider
stream. When no provider status/continuation API proves the terminal outcome,
recovery creates an explicit interrupted/unknown result or makes a separately
accounted retry; it never converts the partial into success.

Retry state records the next attempt, not-before instant, previous normalized
error, retry owner, and logical-attempt identity. Deferred provider state binds
the nonempty remote handle to provider, account, model, API family, and original
request. A process-local delay or poll is never the only evidence that work is
waiting.

## Tool calls

Mutating durable tools require an idempotency key accepted by the effect owner,
or an external status/reconciliation operation. Backend workflow retry settings
MUST NOT override the tool's safety policy.

Durable tool child state distinguishes `planned`, `effect_pending`,
`outcome_ready`, and `completed`. `outcome_ready` owns one complete staged
result and MUST never invoke again, even when an earlier parallel sibling still
blocks source-order publication. Invocation-scoped memos MAY make named safe
substeps replayable; a memo proves only that its value committed, not that an
arbitrary external effect executed exactly once.

## Acceptance scenarios

- Crash after a complete tool outcome is durably staged as `OutcomeReady` but
  before source-order history materialization commits it without reinvoking.
- Crash after an external tool effect may have succeeded but before its outcome
  was durably staged remains an unknown-effect case and follows reconciliation
  or idempotency policy.
- A stale fenced worker cannot append after lease takeover.
- Duplicate wake signals produce one drain of durable admitted input.
- Replay with changed operation schema stops or migrates explicitly.
- A non-idempotent unknown-outcome call requires reconciliation.
- Recovery reconstructs stable state without live delta events.
- Process loss after acceptance but before the first drive starts no duplicate
  effect.
- A staged later tool result materializes without replay after an earlier
  sibling is reconciled.
- Attaching an open operation performs no effect until an explicit drive.
- Every persisted state leaf advances, waits, terminates, or faults; no leaf can
  spin without a durable transition.

## Related specifications

- [Input admission and message queues](input-admission-and-message-queues.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Testing and evaluation](testing-and-evaluation.md)
