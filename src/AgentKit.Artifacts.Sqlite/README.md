# AgentKit.Artifacts.Sqlite

Store artifact content durably in one host-local SQLite database.

Use this backend when finalized bytes and their authoritative metadata must
survive process restart without a server. It runs the same artifact-store
conformance suite as the in-memory and JSON leaves. It advertises durable local
content only: not distributed replication, not cloud-object retention, and not
an atomic transaction with session history. The database file is held
exclusively while the store is open, and every persistence target is explicit
host configuration; no package invents a database path.

## Use this project

Start with `AddSqliteArtifactStore` in
[ServiceExtensions.cs](ServiceExtensions.cs). Read the overloads and XML
documentation for required collaborators, lifetimes, and duplicate-registration
behavior.

Target: **.NET 10**. For a source-checkout setup and a runnable agent, follow
[Getting started](../../docs/getting-started.md). Complete engine composition is
described in the [composition guide](../../docs/guides/composition.md).

## Related projects

- [AgentKit.Artifacts](../AgentKit.Artifacts/README.md) — coordinate bounded
  preparation, finalization, and reading of generated or binary content.
- [AgentKit.Artifacts.InMemory](../AgentKit.Artifacts.InMemory/README.md) — the
  explicitly ephemeral deterministic backend.
- [AgentKit.Artifacts.Json](../AgentKit.Artifacts.Json/README.md) — the
  inspectable local-file backend.

Direct project references:
[AgentKit.Abstractions](../AgentKit.Abstractions/README.md),
[AgentKit.Observability](../AgentKit.Observability/README.md), and
[AgentKit.Storage.Json](../AgentKit.Storage.Json/README.md).

## Tests and reference

- [AgentKit.Artifacts.Sqlite.Tests](../../tests/AgentKit.Artifacts.Sqlite.Tests/README.md)
  — conformance, restart durability, and registration tests.
- [Component specification](../../docs/architecture/artifacts.md) — intended
  ownership and contracts.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
