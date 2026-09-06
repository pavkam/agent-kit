# Sessions, persistence, and branching

**Status:** Normative  
**Depends on:** [Messages](message-and-content-model.md),
[input admission](input-admission-and-message-queues.md)

## Purpose

A session is the durable coordination boundary for related runs. It owns the
ordered record, active branch, admitted input, and optimistic concurrency state.
It is not the same thing as an in-memory agent object.

## Session identity and scope

Every session MUST have a stable `SessionId`, tenant/owner scope, created and
updated times, schema version, and lifecycle state. Each session operation MUST
carry the complete immutable `ExecutionIdentity` captured at admission or run
start plus its matching `SecurityAuthorizationContext`; a tenant/principal pair
is only a routing projection and cannot replace that identity. A
`ConversationId` MAY group several runs or imported histories; it MUST NOT
replace the storage isolation key.

A run MUST declare its session. Standalone runs MAY use an ephemeral session
whose behavior still satisfies ordering and correlation contracts.

## Append-only record

The record is authoritative for both the
[message model](message-and-content-model.md) and
[input admission](input-admission-and-message-queues.md); neither subsystem
keeps a competing durable truth.

The canonical record SHOULD be append-only and contain typed entries such as:

- user, assistant, system, synthetic, and tool messages;
- input admitted and promoted events;
- model/agent/settings changes;
- compaction and context-epoch records;
- goal and delegation transitions;
- security decisions, grants, and approval references; and
- run lifecycle and recovery checkpoints.

Each entry MUST have a stable ID, monotonic sequence, optional parent/causal ID,
timestamp from an injectable clock, and schema version. Stores MUST support
append-if-version so concurrent writers cannot silently overwrite one another.

## Branching

[Context compaction](context-compaction.md) names a range within one branch and
never rewrites that branch's covered entries.

The storage model SHOULD permit entries to name a parent entry rather than
assuming a single irreversible tail. An active branch is the path from a chosen
leaf to the root plus branch-local derived records.

Creating a branch MUST:

- identify an existing committed parent;
- allocate a new branch or leaf identity;
- leave the original branch unchanged;
- rebuild context from the selected path; and
- keep tool results and approvals causal to their original calls.

Editing a past message is represented as a new branch, not an in-place rewrite.

## Snapshot and revert

Snapshots MAY accelerate load or capture application state associated with a
turn. A snapshot MUST name the exact session sequence and content hash it
represents. Loading MUST verify or ignore a stale/corrupt snapshot and replay
the log.

“Revert” changes the active branch pointer or appends a revert record. It MUST
NOT delete later history by default. External side effects already performed are
not undone merely because conversation state moved backward.

## Store contract

Stores provide the evidence required by
[durable recovery](durable-execution-and-recovery.md), including conditional
append, idempotency, versioning, and stable pagination.

`ISessionStore` MUST define:

- create/load with tenant and principal authorization;
- append with expected version and idempotency key;
- branch enumeration and active-leaf update semantics;
- forward pagination by sequence and bounded page size;
- snapshot read/write ownership;
- consistency and transaction guarantees;
- retention, archival, deletion, and legal-hold behavior; and
- conflict, unavailable, corrupt-data, and migration failures.

Session directory access and session store access are separate protected
effects. The coordinator MUST first obtain and consume a short-lived grant bound
to the exact directory lookup or record. After the authoritative store key is
known, it MUST obtain and consume a different grant bound to the exact store,
operation, identity, and resource. A directory grant MUST NOT authorize store
access, and one grant MUST NOT be reused across reads, writes, retries, or
effecting calls. Authority selection uses the captured
`ISecurityAuthoritySelector` binding rather than an unkeyed authority.

Serialization MUST be provider-neutral and preserve unknown fields needed for
forward-compatible round trips.

## Active-run coordination

The execution coordinator owns one active mutating drain per session. A store
lease MAY extend that rule across processes, but a local mutex is not a claim of
cluster safety. Distributed ownership, fencing token, lease expiry, and takeover
must be explicit before multi-process execution is supported.

## Acceptance scenarios

- Concurrent append with the same expected version yields one success and one
  conflict.
- Retried append with the same idempotency key does not duplicate an entry.
- Branching from the middle leaves the original leaf readable and unchanged.
- Snapshot corruption falls back to verified log replay.
- Revert does not claim to reverse external tool side effects.
- Cross-tenant session IDs fail authorization without revealing existence.

## Upstream evidence

- Pi's versioned JSONL session tree, parent IDs, branching, and migration logic
  are centered in
  [`session-manager.ts`](https://github.com/badlogic/pi-mono/blob/9767ba275f3e9a5ee0f5c5342249b629ab1b2282/packages/coding-agent/src/core/session-manager.ts).
- OpenCode V2's local execution coordinator explicitly scopes active drains in
  [`execution/local.ts`](https://github.com/anomalyco/opencode/blob/337fd144d2ba144743368f78d9579a99cce175bd/packages/core/src/session/execution/local.ts).

## Related specifications

- [Context compaction](context-compaction.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Memory, retrieval, and storage](memory-retrieval-and-storage.md)
