# Durable execution and recovery

**Status:** Normative extension boundary  
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

Model requests, tool calls, compaction, external approval waits, and selected
middleware MAY be durable operations. Pure context assembly normally remains a
deterministic replayable function unless expensive enough to checkpoint.

## Checkpoints

The runtime SHOULD checkpoint after these boundaries:

- input admission and promotion;
- context/configuration manifest creation;
- provider response terminal validation;
- tool-call recording before side effects;
- each terminal tool result;
- assistant/tool message commit;
- compaction activation; and
- final run settlement.

Fine-grained token or progress deltas are not required for recovery.

## Recovery classification

After failure, every nonterminal operation MUST be classified:

| Evidence                                          | Recovery                                           |
| ------------------------------------------------- | -------------------------------------------------- |
| No start record / definitely not sent             | Safe to start under policy                         |
| Started with idempotency key and queryable result | Reconcile, then retry/query                        |
| Started, effect unknown, non-idempotent           | Do not retry; require operator/tool reconciliation |
| Terminal result exists, commit missing            | Idempotently commit without reinvocation           |
| Durable external owner accepted handoff           | Resume waiting/query that owner                    |

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

Distributed execution requires one authoritative owner per session or operation.
Leases MUST have expiration, renewal, owner ID, and monotonically increasing
fencing token. Every durable write from an owner verifies the current token so
an expired worker cannot corrupt a resumed run.

Wake signals are hints and MAY coalesce. Durable admitted inputs remain the
source of truth, so a lost or duplicate wake does not lose or duplicate work.

## Provider calls

Provider calls are not inherently replay-safe. An adapter MUST record request
identity before send and use provider idempotency/continuation facilities when
available. If a crash occurs after send without a recoverable response, policy
chooses reconcile, new explicitly duplicated request, or fail; it MUST not
pretend exactly-once delivery.

## Tool calls

Mutating durable tools require an idempotency key accepted by the effect owner,
or an external status/reconciliation operation. Backend workflow retry settings
MUST NOT override the tool's safety policy.

## Acceptance scenarios

- Crash after tool success but before result commit commits without reinvoking.
- A stale fenced worker cannot append after lease takeover.
- Duplicate wake signals produce one drain of durable admitted input.
- Replay with changed operation schema stops or migrates explicitly.
- A non-idempotent unknown-outcome call requires reconciliation.
- Recovery reconstructs stable state without live delta events.

## Upstream evidence

- Pydantic AI isolates durable execution adapters and operation codecs under
  [`durable_exec`](https://github.com/pydantic/pydantic-ai/tree/c0e4d824eaa0401d4481d401e5b3894ab32ab59d/pydantic_ai_slim/pydantic_ai/durable_exec).
- OpenCode V2's current local coordinator explicitly leaves clustered durable
  ownership as a future boundary in
  [`run-coordinator.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/run-coordinator.ts).
- OpenCode records tool calls before local execution in
  [`llm.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/runner/llm.ts).

## Related specifications

- [Input admission and message queues](input-admission-and-message-queues.md)
- [Cancellation, timeouts, and resilience](cancellation-timeouts-and-resilience.md)
- [Testing and evaluation](testing-and-evaluation.md)
