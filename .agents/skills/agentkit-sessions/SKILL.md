---
name: agentkit-sessions
description:
  "Implement or review AgentKit.Session coordination, append-only records,
  branching, store selection, and active-run ownership. Use for durable
  per-session truth and concurrency; not for cross-session memory,
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

## Working rules

1. Treat the session as the canonical ordered record for related runs. Messages
   and admitted inputs must not keep competing durable stores beside it.
2. Keep neutral contracts in `AgentKit.Abstractions`, coordination in
   `AgentKit.Session`, and concrete store clients in leaf packages.
3. Append with expected version and idempotency identity. Preserve stable
   sequence, causality, schema version, typed failures, and authorization scope.
4. Model edit, revert, and fork as branch operations over committed parents.
   Never rewrite history or imply that moving a branch undoes external effects.
5. Enforce one active mutating drain per session. A local lock is process-local;
   distributed ownership requires durability leases and fencing.
6. Select stores explicitly through the singular directory and selector. A
   missing or incompatible durable store never falls back to process memory.

Run the common store conformance suite for ordering, idempotency, optimistic
conflicts, branching, pagination, snapshot fallback, authorization,
cancellation, disposal, and active-run coordination.
