# AgentKit.Processes.Scripted

Simulate process resolution and execution with deterministic scenarios.

Use this backend for tests and replay without starting host processes. It lets
consumers exercise outcomes and bounds independently of an operating-system
sandbox.

## Use this project

Start with `AddScriptedProcesses` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable component
example, follow [Getting started](../../docs/getting-started.md). Complete
engine composition is described in the
[composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Processes](../AgentKit.Processes/README.md) — resolve and run
  operating-system processes through a bounded security boundary.
- [AgentKit.Tools.Command](../AgentKit.Tools.Command/README.md) — offer an
  explicit shell-command tool over the process boundary.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Processes.Scripted.Tests](../../tests/AgentKit.Processes.Scripted.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/process-execution.md) —
  intended ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
