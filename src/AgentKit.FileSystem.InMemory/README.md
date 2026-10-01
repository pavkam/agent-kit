# AgentKit.FileSystem.InMemory

A deterministic, disk-free implementation of the filesystem contract for tests
and ephemeral hosts.

Use this in place of `AgentKit.FileSystem` when a component needs a fast,
hermetic keyed file-system double: unit and integration tests, sandboxed
execution environments without real disk access, or any host that wants
workspace state to disappear with the process. It proves the same `IFileReader`,
`IFileWriter`, `IFileMetadataReader`, `IDirectoryCreator`, `IDirectoryReader`,
`IFileGlobber`, `IFileContentSearcher`, `IFileSnapshotReader`,
`IAtomicFileReplacer`, and `IWorkspacePatchApplier` contracts as the
operating-system profile, including grant validation, byte and traversal bounds,
and honest per-entry patch settlement. There are no symbolic links in the
virtual tree, so the boundary-crossing failure modes that exist purely to defend
a real filesystem against symlink traversal do not apply here.

## Use this project

Start with the keyed `AddInMemoryFileSystem(key)` in
[ServiceExtensions.cs](ServiceExtensions.cs). No configured root directory is
required: every path resolves against a process-local virtual tree that exists
only for the lifetime of the registered `InMemoryFileSystem` instance.

A directory can only come to exist through the authorized `IDirectoryCreator`
capability, [`CreateDirectory`](InMemoryFileSystem.cs), or as an implicit parent
of a path [`Seed`](InMemoryFileSystem.cs) installs. The last two bypass grant
validation entirely: they play the same role that directly writing to the
operating-system profile's root directory plays in that implementation's own
tests. Every subsequent capability call against the resulting tree is fully
protected.

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
- [Workstreams](../../docs/workstreams/index.md) — how this component was built,
  chunk by chunk.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
