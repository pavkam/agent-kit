# AgentKit.Artifacts.FileSystem

Store artifact content through AgentKit's protected file-system contracts.

Unlike the SQLite and JSON leaves, this backend never opens a host path itself:
every read and write is a typed security request whose single-use grant the
selected `IFileReader` or `IFileWriter` revalidates and consumes, and every
write names an explicit disposition. Entry state is a flushed newline-delimited
log replayed through the planner the other backends share; payloads are
content-addressed files partitioned per tenant. The contracts define no delete
and no enumeration, so a released payload is truncated rather than removed and
crash leftovers are not swept. The store creates no directories and holds no
lock, so the root must exist and have one writer. It advertises durability only
to the extent the selected file-system profile provides it.

## Use this project

Start with `AddFileSystemArtifactStore` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Artifacts](../AgentKit.Artifacts/README.md) — coordinate bounded
  preparation, finalization, and reading of generated or binary content.
- [AgentKit.FileSystem](../AgentKit.FileSystem/README.md) — the operating-system
  file-system profile a host can select.
- [AgentKit.FileSystem.InMemory](../AgentKit.FileSystem.InMemory/README.md) —
  the deterministic in-memory profile the conformance suite runs over.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md), and
[AgentKit.Storage.Json](../AgentKit.Storage.Json/README.md).

## Tests and reference

- [AgentKit.Artifacts.FileSystem.Tests](../../tests/AgentKit.Artifacts.FileSystem.Tests/README.md)
  — conformance and protected-effect tests.
- [Component specification](../../docs/architecture/artifacts.md) — intended
  ownership and contracts.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
