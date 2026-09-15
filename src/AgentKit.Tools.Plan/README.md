# AgentKit.Tools.Plan

Maintain versioned planning state through session-backed tool operations.

Use this tool when an agent needs an explicit plan that can be updated and
checked for stale writes. Plan state is separate from a prompt's prose.

## Use this project

Start with `AddPlanTool` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.Session](../AgentKit.Session/README.md) — coordinate session
  lifecycle, run ownership, branching, and store routing.
- [AgentKit.Tools.Task](../AgentKit.Tools.Task/README.md) — request bounded task
  delegation through the delegation broker.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Plan.Tests](../../tests/AgentKit.Tools.Plan.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
