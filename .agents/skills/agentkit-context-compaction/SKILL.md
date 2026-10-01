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
4. Keep generated prose untrusted after structural validation. Authoritative
   checkpoint fields derive only from committed source records; structural
   validation does not prove semantic entailment of arbitrary summary text.
5. Generate outside the append lock, then activate with optimistic concurrency.
   A stale source produces a conflict; never silently rebase or lose an append.
6. Keep the model-backed strategy a preparer of a bounded
   `CompactionSummaryRequest`; `ICompactionSummaryGenerator`, resolved per
   compactor key, owns the provider interaction. Registration is keyed per
   compactor: `AddAgentContextCompaction` plus the additive `Add*` helpers for
   strategies, summary generators, and event sinks, one `Replace*` per singular
   collaborator, and `AddCompactionProfile`, whose catalog compiles the
   `CompactionPolicySnapshot` that engine composition validates and the loop
   attaches. Duplicate identities fail unless the exact `Replace*` is used;
   never add an unkeyed collaborator or a `Scoped` strategy, generator, or sink.
7. Before activation, failure leaves the previous path active. After activation,
   preserve the committed record and report truthful or unknown commit state.
   Bound non-reducing attempts and return typed context-limit outcomes.

Verify safe cuts, forged-state rejection, exact suffix replay, concurrent
append, idempotent activation, truthful cancellation commit state, bounded
reduction, strategy isolation, and original-history availability.
