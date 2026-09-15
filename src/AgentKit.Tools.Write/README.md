# AgentKit.Tools.Write

Write file content through an authorized filesystem boundary.

Use this tool for bounded writes with explicit target-state semantics. Select
the required `mode` as `create_only`, `replace_existing`, `create_or_replace`,
or `append`; `create_new` and `overwrite` remain explicit legacy aliases.
`replace_existing` fails without mutation when the target is missing. Empty and
whitespace-only content are valid. Parent-directory creation requires its own
separate authorization.

## Use this project

Start with `AddWriteTool` in [ServiceExtensions.cs](ServiceExtensions.cs). Read
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
- [AgentKit.Tools.Edit](../AgentKit.Tools.Edit/README.md) — replace exact text
  under version conditions and filesystem bounds.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.Tools.Write.Tests](../../tests/AgentKit.Tools.Write.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/tools.md) — intended
  ownership and contracts.
- [Coding-harness tools](../../docs/profiles/coding-harness/coding-harness-built-in-tools.md)
  — the application profile for these features.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
