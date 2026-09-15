# AgentKit.FileSystem

Access files through a root-bounded implementation of the filesystem contract.

Use this host adapter behind file tools. Configure the allowed roots and
security authority; filesystem access remains a protected effect even when a
tool has already been authorized.

## Use this project

Start with `AddSandboxedFileSystem` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.FileSystem.InMemory](../AgentKit.FileSystem.InMemory/README.md) — a
  deterministic, disk-free implementation of the same contract for tests and
  ephemeral hosts.
- [AgentKit.Tools.Read](../AgentKit.Tools.Read/README.md) — read bounded file
  content through the filesystem abstraction.
- [AgentKit.Tools.Write](../AgentKit.Tools.Write/README.md) — write file content
  through an authorized filesystem boundary.
- [AgentKit.Tools.Edit](../AgentKit.Tools.Edit/README.md) — replace exact text
  under version conditions and filesystem bounds.
- [AgentKit.Permissions](../AgentKit.Permissions/README.md) — evaluate security
  requests and manage bounded grants, profiles, and audit dispatch.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md). Other related
projects above are composition collaborators, not necessarily dependencies.

## Tests and reference

- [AgentKit.FileSystem.Tests](../../tests/AgentKit.FileSystem.Tests/README.md) —
  focused behavior and registration tests.
- [Component specification](../../docs/architecture/file-system.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
