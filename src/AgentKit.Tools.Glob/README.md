# AgentKit.Tools.Glob

Match workspace paths using deterministic in-process globbing.

Use this tool to discover files within an authorized scope. File content search
belongs to the separate Search tool.

## Use this project

Start with `AddGlobTool` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
the overloads and XML documentation for required collaborators, lifetimes, and
duplicate-registration behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Tools](../AgentKit.Tools/README.md) — catalog, validate, authorize,
  and invoke application tools.
- [AgentKit.FileSystem](../AgentKit.FileSystem/README.md) — access files through
  a root-bounded implementation of the filesystem contract.
- [AgentKit.Tools.Search](../AgentKit.Tools.Search/README.md) — search file
  content deterministically within a bounded workspace scope.
- [AgentKit.Tools.List](../AgentKit.Tools.List/README.md) — list directories
  with bounded, snapshot-based pagination.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Glob.Tests](../../tests/AgentKit.Tools.Glob.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
