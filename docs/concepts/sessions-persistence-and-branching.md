# Sessions, persistence, and branching

**Status:** Normative

**Architecture:** [Sessions](../architecture/sessions.md)

**Depends on:** [Messages](message-and-content-model.md)

## Purpose

A session is the durable coordination boundary for related runs. It owns the
immutable conversation tree, named branches, execution-lane state, admitted
input, current orchestration projection, usage evidence, and optimistic
concurrency state. It is not the same thing as an in-memory agent object.

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

The run plan MUST freeze the selected session profile as an immutable,
positively versioned snapshot containing its coordinator keys, default store,
retention policy, concurrency behavior, bounds, and configuration fingerprint.
An invocation-only capability binds that snapshot to the exact coordinator and
run coordinator. History, tools, compaction, and other session-writing
components receive the same capability; they MUST NOT rediscover an unkeyed
coordinator or observe a newer profile during the operation.

## Append-only record

The record is authoritative for both the
[message model](message-and-content-model.md) and
[input admission](input-admission-and-message-queues.md); neither subsystem
keeps a competing durable truth.

The canonical semantic record SHOULD be append-only and contain typed entries
such as:

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

An entry's sequence MUST be monotonic only within its own branch: it is that
branch's 1-based commit position, allocated independently of every sibling
branch of the same session. A store MUST accept an append batch only when its
sequences are contiguous starting at the target branch's own current tip plus
one; the count of entries committed on a different branch of the same session
MUST NOT influence that computation or that append's acceptance. The
whole-session `SessionVersion` compare-and-swap token remains distinct from
sequence: it advances by exactly one per committed mutation regardless of which
branch the mutation targets, so two sibling branches' sequence coordinates may
coincide numerically without naming related entries. An entry's ID, not the pair
of branch and sequence, is its stable identity across every branch that retains
a copy of it after a fork.

Current branch tips, lane configuration, inboxes, leases, total operation state,
bounded progress checkpoints, and staged outcomes MAY use replaceable typed
state records. Replacing current state MUST NOT erase semantic history or usage
evidence. Every such record declares one owner, lifecycle, fork policy, and
terminal cleanup transaction. Derived indexes and statistics are rebuildable and
never prove effect completion.

Usage is an append-only ledger separate from recovery state. Every settled
provider attempt retains native counters and cost provenance even when retried
or when its enclosing run later aborts. Recovery does not infer control flow
from billing rows.

## Branching

[Context compaction](context-compaction.md) names a range within one branch and
never rewrites that branch's covered entries.

The storage model SHOULD permit entries to name a parent entry rather than
assuming a single irreversible tail. An active branch is the path from a chosen
leaf to the root plus branch-local derived records.

A branch owns data and a tip. An execution lane binds total agent configuration,
input queues, and at most one current operation to one branch. Multiple lanes
MAY share immutable ancestry and run effects concurrently; they MUST NOT share
an unfenced movable tip.

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
- stable prefix pagination: the first page captures address, branch, session
  version, and inclusive upper sequence, and every continuation echoes that
  snapshot while excluding later appends;
- snapshot read/write ownership;
- consistency and transaction guarantees;
- retention, archival, deletion, and legal-hold behavior; and
- conflict, unavailable, corrupt-data, and migration failures.

Multi-record transitions that publish an entry, move a tip, settle usage, and
advance total operation state MUST be atomic or expose an equivalent idempotent
commit protocol. Recovery reads one complete current operation state; it MUST
NOT deduce the restart point from absent auxiliary rows.

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

The session runtime MUST NOT register a concrete store or directory as a hidden
fallback. Hosts explicitly select an `AgentKit.Session.InMemory`,
`AgentKit.Session.Sqlite`, `AgentKit.Session.Json`, or other adapter and a
compatible directory. The in-memory, SQLite, and JSON leaves run the same store
conformance suite. SQLite may advertise durable local transactions after restart
tests pass, but it MUST NOT advertise distributed lane ownership, fencing, or
cross-store atomicity merely because the session rows share one local database.
The JSON leaf recovers optimistic concurrency, per-lane ownership, and every
idempotency receipt by replaying its command log through the same commit path
that produced it, and it reuses the session runtime's portable entry codec so
entries stay interchangeable with the SQLite leaf.

## Discovery and migration

The session profile declares when the first durable record is created. A host
that acknowledges durable admission MUST persist it immediately; it cannot keep
user-only work and early metadata solely in memory until an assistant happens to
reply.

Discovery is a bounded projection, not authoritative loading. It defines header
scan limits, unreadable/oversized behavior, activity-time meaning, and whether
an empty candidate is eligible. Authoritative open validates the complete
record. A torn final transaction may be discarded only as a whole under the
backend contract; a malformed interior record is corruption and MUST NOT be
silently skipped. Orphans, self-parent entries, duplicate IDs, and impossible
tips are reported or repaired through an explicit versioned policy, never
quietly promoted into a new tree shape.

Schema migration is a protected atomic maintenance operation with a backup or
recoverable replacement strategy. Opening a session MUST NOT truncate and
rewrite its file as an incidental read. Multi-process writers require CAS or a
lease/fence; append-only syntax alone supplies neither.

Activity and statistics declare their scope: whole tree, selected branch,
execution lane, current operation, or provider request. File modification time,
last conversational activity, label changes, abandoned-branch usage, and
compacted-history usage are distinct projections.

## Branch selection and extraction

Branch selection and extraction are distinct:

- selection moves a tip in the same session and preserves later branches; and
- extraction creates a new session containing one selected ancestry path and
  repairs every retained causal and compaction reference.

Neither operation moves or reverses external effects. Application workspace
placement, editor restoration, labels, bookmarks, and user-facing lookup rules
belong to the host or an application profile rather than the session contract.

## Mutation and operation coordination

The execution coordinator owns one active operation per execution lane. The
session coordinator also owns one serialized mutation line, transaction queue,
or equivalent optimistic protocol for durable read-decide-write transitions
across all of that session's lanes. Ordinary stable reads need not block on a
long-running provider or tool effect.

Exactly one host owns writable session coordination at a time unless a
distributed profile supplies leases and fencing. A process-local mutex is not a
claim of cluster safety. Distributed ownership, fencing token, lease expiry, and
takeover must be explicit before multi-process execution is supported.

Invocation cancellation releases or stops one caller observation. Durable abort
names the expected operation and is a separate session mutation. Neither may be
implemented as an ambient cancellation token stored on the session object.

An execution lane accepts at most one run at a time. The store MUST expose an
explicit release transaction that clears a lane's installed accepted state once
its owning operation and run have completely settled, so a later start on the
same lane no longer observes a busy result. Release MUST name the exact
operation, run, and total-state revision it owns and MUST clear the lane only
when that evidence matches the lane's actual installed occupant; a stale caller
MUST NOT be able to clear a different, newer occupant. Release MUST carry an
idempotency key so a retried release after a lost response returns the original
receipt rather than a second commit, and MUST be masked as not-found for a
missing or cross-tenant session exactly like every other protected session
operation. A caller's disposal of local process ownership and a caller's
explicit durable release are distinct: local disposal alone MUST NOT clear
durable accepted state, since a crash or handoff may still need to recover and
reacquire the identical accepted operation.

## Acceptance scenarios

- Concurrent append with the same expected version yields one success and one
  conflict.
- Retried append with the same idempotency key does not duplicate an entry.
- Branching from the middle leaves the original leaf readable and unchanged.
- Snapshot corruption falls back to verified log replay.
- Revert does not claim to reverse external tool side effects.
- Cross-tenant session IDs fail authorization without revealing existence.
- Two lanes sharing ancestry can overlap effects without losing either branch
  append.
- Releasing a lane's accepted run allows a later start on that lane to succeed
  instead of observing a busy result.
- A release naming a different run than the lane's actual occupant is rejected
  as fenced rather than clearing the real owner.
- Recovery resumes from total current operation state rather than folding a
  partial write journal.
- A malformed interior record fails open without rewriting the source.
- Extracting a branch repairs retained references and excludes open-operation
  state unless a durable handoff protocol is explicitly selected.
- A missing explicit store or directory fails composition without probing or
  creating an in-memory session route.
- In-memory and SQLite stores produce the same ordered/idempotent results for
  shared operations; only SQLite retains committed local state after reopen.

## Related specifications

- [Context compaction](context-compaction.md)
- [Durable execution and recovery](durable-execution-and-recovery.md)
- [Memory, retrieval, and storage](memory-retrieval-and-storage.md)
