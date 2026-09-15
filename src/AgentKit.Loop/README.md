# AgentKit.Loop

Coordinate turns, context preparation, model attempts, tool calls, and terminal
outcomes.

Use the loop implementation with explicitly selected providers, context, tools,
session coordination, and output processing. Continuation policy is a separate
replaceable service.

## Use this project

Start with `AddAgentLoop`, `AddRunContinuationPolicy` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Output](../AgentKit.Output/README.md) — resolve output definitions
  and validate, repair, or deserialize terminal candidates.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Loop.Tests](../../tests/AgentKit.Loop.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/agent-runtime.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
