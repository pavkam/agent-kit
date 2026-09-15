# AgentKit.Hooks

Dispatch typed lifecycle hooks with ordering, mutation validation, and failure
policy.

Use hooks to observe or customize the boundaries that explicitly permit it. Hook
registrations do not grant security authority or replace the component that owns
an operation.

## Use this project

Start with `AddAgentHooks` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Abstractions](../AgentKit.Abstractions/README.md) — implement
  AgentKit extensions against provider-neutral contracts and typed domain
  values.
- [AgentKit.Loop](../AgentKit.Loop/README.md) — coordinate turns, context
  preparation, model attempts, tool calls, and terminal outcomes.
- [AgentKit.Observability](../AgentKit.Observability/README.md) — share AgentKit
  logging, activity, metric, and tag conventions across components.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Hooks.Tests](../../tests/AgentKit.Hooks.Tests/README.md) — focused
  behavior and registration tests.
- [Component specification](../../docs/architecture/extensions.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
