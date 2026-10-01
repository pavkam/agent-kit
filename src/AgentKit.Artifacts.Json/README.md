# AgentKit.Artifacts.Json

Store artifact content durably as inspectable local files.

Entry metadata is a flushed newline-delimited JSON log; payload bytes are
content-addressed files written atomically, partitioned per tenant so identical
bytes in two tenants are never one file. It runs the same artifact-store
conformance suite as the in-memory and SQLite leaves. The store holds an
advisory exclusive lock on its root and rejects a second writer, recovers a torn
trailing append on request, and claims no multi-process coordination and no
atomicity with session history. The root is explicit host configuration; no
package invents a path.

## Use this project

Start with `AddJsonArtifactStore` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Artifacts](../AgentKit.Artifacts/README.md) — coordinate bounded
  preparation, finalization, and reading of generated or binary content.
- [AgentKit.Artifacts.Sqlite](../AgentKit.Artifacts.Sqlite/README.md) — the
  transactional local database backend.
- [AgentKit.Storage.Json](../AgentKit.Storage.Json/README.md) — shared JSON
  storage mechanics.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md), and
[AgentKit.Storage.Json](../AgentKit.Storage.Json/README.md).

## Tests and reference

- [AgentKit.Artifacts.Json.Tests](../../tests/AgentKit.Artifacts.Json.Tests/README.md)
  — conformance, restart durability, and recovery tests.
- [Component specification](../../docs/architecture/artifacts.md) — intended
  ownership and contracts.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
