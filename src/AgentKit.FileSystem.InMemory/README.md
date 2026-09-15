# AgentKit.FileSystem.InMemory

A deterministic, disk-free implementation of the filesystem contract for tests
and ephemeral hosts.

Use this in place of `AgentKit.FileSystem` when a component needs a fast,
hermetic `IFileSystem` double: unit and integration tests, sandboxed execution
environments without real disk access, or any host that wants workspace state to
disappear with the process. It proves the same `IFileSystem`,
`IDirectoryReader`, `IFileGlobber`, `IFileContentSearcher`,
`IFileSnapshotReader`, `IAtomicFileReplacer`, and `IWorkspacePatchApplier`
contracts as the sandboxed implementation, including grant validation, byte and
traversal bounds, and honest per-entry patch settlement. There are no symbolic
links in the virtual tree, so the boundary-crossing failure modes that exist
purely to defend a real filesystem against symlink traversal do not apply here.

## Use this project

Start with `AddInMemoryFileSystem` in
[ServiceExtensions.cs](ServiceExtensions.cs). No configured root directory is
required: every path resolves against a process-local virtual tree that exists
only for the lifetime of the registered `InMemoryFileSystem` instance.

There is no directory-creation member on any of the implemented contracts, so a
directory can only come to exist through
[`CreateDirectory`](InMemoryFileSystem.cs) or as an implicit parent of a path
[`Seed`](InMemoryFileSystem.cs) installs. Both bypass grant validation entirely:
they play the same role that directly writing to the sandbox's configured root
directory (outside any `SandboxedFileSystem` instance method) plays in that
implementation's own tests. Every subsequent `IFileSystem` effect against the
resulting tree is fully protected.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.FileSystem](../AgentKit.FileSystem/README.md) — the sandboxed,
  root-jailed default backed by real disk.
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

- [AgentKit.FileSystem.InMemory.Tests](../../tests/AgentKit.FileSystem.InMemory.Tests/README.md)
  — focused behavior and registration tests.
- [Component specification](../../docs/architecture/file-system.md) — intended
  ownership and contracts.
- [Implementation status](../../docs/implementation-progress.md#component-coverage)
  — remaining architecture work and proof.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
