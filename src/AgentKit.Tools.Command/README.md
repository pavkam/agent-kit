# AgentKit.Tools.Command

Offer an explicit shell-command tool over the process boundary.

Use it when an agent needs bounded command execution. Select a process backend
and an authorized command profile; registering the tool does not permit
arbitrary host execution.

## Use this project

Start with `AddCommandTool` in [ServiceExtensions.cs](ServiceExtensions.cs).
Read the overloads and XML documentation for required collaborators, lifetimes,
and duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Processes](../AgentKit.Processes/README.md) — resolve and run
  operating-system processes through a bounded security boundary.
- [AgentKit.Processes.Scripted](../AgentKit.Processes.Scripted/README.md) —
  simulate process resolution and execution with deterministic scenarios.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Command.Tests](../../tests/AgentKit.Tools.Command.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
