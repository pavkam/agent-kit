# AgentKit.Artifacts.Sqlite.Tests

Focused tests for
[AgentKit.Artifacts.Sqlite](../../src/AgentKit.Artifacts.Sqlite/README.md).

**Component purpose:** store artifact content durably in one host-local SQLite
database.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [SqliteArtifactStoreTests](SqliteArtifactStoreTests.cs)
- [SqliteArtifactStoreConformanceTests](SqliteArtifactStoreConformanceTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Artifacts.Sqlite.Tests/AgentKit.Artifacts.Sqlite.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Artifacts.Sqlite](../../src/AgentKit.Artifacts.Sqlite/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/artifacts.md) — intended
  contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
