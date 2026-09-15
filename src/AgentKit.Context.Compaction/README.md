# AgentKit.Context.Compaction

Select safe history cuts and produce, validate, and activate compaction
checkpoints.

Use compaction when older context needs a bounded representation. The current
strategy is deterministic and extractive; it does not require a summarization
model.

## Use this project

Start with `AddContextCompaction` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

## Behavioral guarantees

- `SourceThrough` is an eligibility bound: the compactor reads the branch to
  its tip, hands collaborators only entries at or below the bound, and
  allocates the activated entry's sequence after the real tip.
- Source pages are pinned to one `SessionReadSnapshot`; a request whose
  `SourceVersion` differs from the observed version returns
  `CompactionConflict` before any strategy runs.
- `CompactionId` is reconciled by identity: a replayed request, a racing
  duplicate, or a lost append response resolves to the committed record
  instead of a reused-idempotency-key failure.
- Cancellation returns `CompactionCancelled` with a truthful
  `CompactionCommitState`; a committed record is never rolled back.
- The validator re-derives covered identities, range, retained suffix start,
  and manifest claims from the source before activation.
- The structural cut selector prefers a boundary whose retained suffix begins
  at a user turn and never splits a recorded causal pairing.
- `Deadline` is enforced against the injected `TimeProvider` before any
  session I/O; `TargetInputTokens` is advisory.
- `CompactionFailure.Retryable` is `true` only for a transient source read or a
  drifted continuation snapshot.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Budgets](../AgentKit.Budgets/README.md) — reserve and account for
  capacity across hierarchical budget scopes.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Context.Compaction.Tests](../../tests/AgentKit.Context.Compaction.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/context-compaction.md) —
  intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
