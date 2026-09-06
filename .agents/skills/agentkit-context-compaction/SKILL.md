---
name: agentkit-context-compaction
description:
  "Implement or debug AgentKit.Context.Compaction cut selection, summary
  generation or validation, and activation. Use for bounded semantic checkpoints
  over older context; not for deleting history, ordinary request assembly, or
  general session persistence."
---

# AgentKit Context Compaction

Read [AGENTS.md](../../../AGENTS.md). For C# API or implementation work, also
read the [modern C# rules](../references/modern-csharp.md).

## Authoritative design

- Read
  [context compaction architecture](../../../docs/architecture/context-compaction.md)
  for contracts, strategy composition, activation, DI, and failure outcomes.
- Read the normative
  [context compaction specification](../../../docs/concepts/context-compaction.md)
  for trigger, cut, record, summary, concurrency, and retry semantics.
- Read
  [sessions, persistence, and branching](../../../docs/concepts/sessions-persistence-and-branching.md)
  when changing source reads or activation behavior.

## Working rules

1. Keep neutral contracts in `AgentKit.Abstractions` and first-party mechanics
   in `AgentKit.Context.Compaction`; never make the compactor call the
   assembler.
2. Read one authorized stable branch version and choose a complete semantic cut.
   Preserve tool, approval, deferred, goal, and admitted-input causality.
3. Retain the exact suffix and a source manifest. Covered history remains
   queryable, branchable, auditable, and eligible for recompaction.
4. Treat every generated summary as untrusted until structural, causal,
   security, provenance, and measurable-reduction validation succeeds.
5. Generate outside the append lock, then activate with optimistic concurrency.
   A stale source produces a conflict; never silently rebase or lose an append.
6. Failure or cancellation leaves the previous context path active. Bound
   non-reducing attempts and return typed context-limit outcomes.

Verify safe cuts, forged-state rejection, exact suffix replay, concurrent
append, idempotent activation, truthful cancellation commit state, bounded
reduction, strategy isolation, and original-history availability.
