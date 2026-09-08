# AgentKit.Artifacts

Coordinate bounded preparation, finalization, and reading of generated or binary
content.

Use artifact references for content that should live outside conversation
messages. Select a backing store explicitly; callers coordinate commitment of
references into session or tool records.

## Use this project

Start with `AddAgentArtifacts` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Artifacts.InMemory](../AgentKit.Artifacts.InMemory/README.md) —
  store artifact content in memory with deterministic lifecycle behavior.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Artifacts.Tests](../../tests/AgentKit.Artifacts.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/artifacts.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
