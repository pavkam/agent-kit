# AgentKit.Abstractions

Implement AgentKit extensions against provider-neutral contracts and typed
domain values.

Reference this package when supplying an alternative implementation or
integration. It defines contracts shared by engines and components without
depending on their implementations.

## Use this project

Start with the contracts for the component you are implementing. The
[foundation specification](../../docs/architecture/foundation-contracts.md) maps
the shared values and extension boundaries. Component implementations provide
their own DI registration methods.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit](../AgentKit/README.md) — compose a process-level engine that hosts
  immutable agent definitions and isolated runs.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Providers](../AgentKit.Providers/README.md) — catalog configured
  models, validate capabilities, and select or resolve model implementations.

This project has no source-project dependencies.

## Tests and reference

- [AgentKit.Abstractions.Tests](../../tests/AgentKit.Abstractions.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/foundation-contracts.md) —
  intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
