---
name: agentkit-sessions
description:
  "Implement or review AgentKit.Session coordination, append-only records,
  branching, store selection, execution lanes, and operation ownership. Use for
  durable per-session truth and concurrency; not for cross-session memory,
  request-context selection, or durability-backend recovery."
---

# AgentKit Sessions

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read [sessions architecture](../../../docs/architecture/sessions.md) for
  coordination, store selection, contract shape, DI, and package ownership.
- Read the normative
  [sessions, persistence, and branching](../../../docs/concepts/sessions-persistence-and-branching.md)
  specification for record, concurrency, branching, and store semantics.
- Read
  [input admission and message queues](../../../docs/concepts/input-admission-and-message-queues.md)
  when changing admitted-input records or promotion.
- Read the
  [coding-harness execution profile](../../../docs/profiles/coding-harness/coding-harness-execution-profile.md)
  for lanes and operation-owned state, and
  [workspace snapshots and reversion](../../../docs/profiles/coding-harness/workspace-snapshots-and-reversion.md)
  when session navigation must be distinguished from filesystem restoration.
- Read
  [export, sharing, and control plane](../../../docs/profiles/coding-harness/coding-harness-export-sharing-and-control-plane.md)
  for session export/import, durable sharing outboxes, or host routing.

## Working rules

1. Treat the session as the canonical ordered record for related runs. Messages
   and admitted inputs must not keep competing durable stores beside it.
2. Keep neutral contracts in `AgentKit.Abstractions`, coordination in
   `AgentKit.Session`, and concrete store clients in leaf packages.
3. Append with expected version and idempotency identity. Preserve stable
   sequence, causality, schema version, typed failures, and authorization scope.
4. Model edit, revert, and fork as branch operations over committed parents.
   Never rewrite history or imply that moving a branch undoes external effects.
5. Enforce one active operation per execution lane and one serialized session
   mutation line across lanes. Different lanes may overlap effects; they never
   perform unfenced concurrent branch-tip commits. A local lock is
   process-local; distributed ownership requires durability leases and fencing.
6. Select stores explicitly through the singular directory and selector. A
   missing or incompatible durable store never falls back to process memory.

Run the common store conformance suite for ordering, idempotency, optimistic
conflicts, branching, pagination, snapshot fallback, authorization,
cancellation, disposal, per-lane operation ownership, and cross-lane mutation
serialization.
