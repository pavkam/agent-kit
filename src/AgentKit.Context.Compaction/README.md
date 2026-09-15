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
