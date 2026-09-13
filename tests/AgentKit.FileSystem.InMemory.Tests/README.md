# AgentKit.FileSystem.InMemory.Tests

Focused tests for
[AgentKit.FileSystem.InMemory](../../src/AgentKit.FileSystem.InMemory/README.md).

**Component purpose:** a deterministic, disk-free implementation of the
filesystem contract for tests and ephemeral hosts.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [InMemoryFileSystemTests](InMemoryFileSystemTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.FileSystem.InMemory.Tests/AgentKit.FileSystem.InMemory.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.FileSystem.InMemory](../../src/AgentKit.FileSystem.InMemory/README.md)
  — implementation and registration entry points.
- [AgentKit.FileSystem](../../src/AgentKit.FileSystem/README.md) — the
  sandboxed, root-jailed default this project proves equivalent behavior
  against.
- [Component specification](../../docs/architecture/file-system.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
