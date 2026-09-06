# Sessions

**Role:** Provide the durable coordination boundary for related runs.

A session owns an ordered record, active branch, admitted input, configuration
and context transitions, and optimistic concurrency state. It is not an
in-memory agent instance. Even a standalone run uses an ephemeral session with
the same ordering and correlation semantics.

## Canonical record

The session record is append-oriented and versioned. It contains messages, input
admission and promotion, lifecycle transitions, model and configuration changes,
tool calls and results, permissions and approval references, compaction, goals,
delegation, and recovery checkpoints.

Every entry has stable identity, monotonic sequence, causal linkage, timestamp,
and schema version. Appends use an expected version and idempotency identity so
concurrent writers cannot silently overwrite one another and retries cannot
duplicate facts.

## Branching and compaction

Branches name a committed parent and create a new leaf without changing the
original path. Editing an earlier message, changing direction, or reverting
creates branch state rather than rewriting history. Tool effects remain causal
to their original calls; moving the conversation pointer backward does not undo
the outside world.

Compaction appends a versioned summary and structured checkpoint over a complete
semantic range while preserving the covered entries. The active request view
uses the applicable summary plus the exact suffix. Failed or stale compaction
never deletes the previous path.

Snapshots may accelerate loading, but each names an exact sequence and content
hash. A stale or corrupt snapshot is ignored in favor of verified log replay.

## Store boundary

The session store defines authorization, create and load, conditional append,
pagination, branches, snapshots, consistency, transactions, retention, archival,
deletion, migration, and failure behavior. Serialization is provider- neutral
and preserves compatible unknown fields.

Storage client types stay in leaf packages. The runtime interacts only with the
session contract and does not keep session state forever in a shared singleton.

## Active-run ownership

The default coordinator permits one active mutating run per session. New work
must explicitly join, queue, wait, or fail while the session is busy. A local
lock provides process-local coordination only. Cross-process ownership requires
the durable execution component's leases and fencing.

Conversation history belongs here. Durable memory across sessions belongs to the
memory component. The working provider context belongs to the context component.
Combining them into one cheerful bucket called memory would destroy their policy
and consistency boundaries.

## Related concept specifications

- [Sessions, persistence, and branching](../concepts/sessions-persistence-and-branching.md)
- [Context compaction](../concepts/context-compaction.md)
- [Input admission and message queues](../concepts/input-admission-and-message-queues.md)
