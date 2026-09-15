# AgentKit.Artifacts.InMemory

Store artifact content in memory with deterministic lifecycle behavior.

Use this backend for tests and ephemeral applications that need the
artifact-store contract. Content does not survive process loss.

## Use this project

Start with `AddInMemoryArtifactStore` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Artifacts](../AgentKit.Artifacts/README.md) — coordinate bounded
  preparation, finalization, and reading of generated or binary content.
- [AgentKit.Session.InMemory](../AgentKit.Session.InMemory/README.md) — keep
  session records and directory state in memory for ephemeral applications.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Artifacts.InMemory.Tests](../../tests/AgentKit.Artifacts.InMemory.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/artifacts.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
