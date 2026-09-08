# AgentKit.Context

Assemble provider-ready context while preserving message trust and tool-call
correlation.

Use the context assembler to prepare bounded request history and instructions.
Session storage owns durable conversation records; this package prepares the
view used for a request.

## Use this project

Start with `AddAgentContext` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Context.Compaction](../AgentKit.Context.Compaction/README.md) —
  select safe history cuts and produce, validate, and activate compaction
  checkpoints.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Context.Tests](../../tests/AgentKit.Context.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/context.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
