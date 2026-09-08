# AgentKit.Processes

Resolve and run operating-system processes through a bounded security boundary.

Use this adapter behind command tools. Configure executable resolution, sandbox
enforcement, and process limits explicitly; process lifetime and termination are
part of the result contract.

## Use this project

Start with `AddOperatingSystemProcesses` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Processes.Scripted](../AgentKit.Processes.Scripted/README.md) —
  simulate process resolution and execution with deterministic scenarios.
- [AgentKit.Tools.Command](../AgentKit.Tools.Command/README.md) — offer an
  explicit shell-command tool over the process boundary.
- [AgentKit.Artifacts](../AgentKit.Artifacts/README.md) — coordinate bounded
  preparation, finalization, and reading of generated or binary content.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Processes.Tests](../../tests/AgentKit.Processes.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/process-execution.md) —
  intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
