# AgentKit.Session.InMemory

Keep session records and directory state in memory for ephemeral applications.

Use this store for tests, examples, and short-lived workloads. Session data is
process-local and cannot be used as evidence of recovery after a restart.

## Use this project

Start with `AddInMemorySessionStore`, `AddInMemorySessionDirectory` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Artifacts.InMemory](../AgentKit.Artifacts.InMemory/README.md) —
  store artifact content in memory with deterministic lifecycle behavior.
- [AgentKit.Context](../AgentKit.Context/README.md) — assemble provider-ready
  context while preserving message trust and tool-call correlation.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Session.InMemory.Tests](../../tests/AgentKit.Session.InMemory.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/sessions.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
