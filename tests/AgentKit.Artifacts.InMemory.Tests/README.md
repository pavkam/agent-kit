# AgentKit.Artifacts.InMemory.Tests

Focused tests for
[AgentKit.Artifacts.InMemory](../../src/AgentKit.Artifacts.InMemory/README.md).

**Component purpose:** store artifact content in memory with deterministic
lifecycle behavior.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [InMemoryArtifactStoreConformanceTests](InMemoryArtifactStoreConformanceTests.cs)
- [InMemoryArtifactStoreTests](InMemoryArtifactStoreTests.cs)
- [TenantArtifactKeysTests](TenantArtifactKeysTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Artifacts.InMemory.Tests/AgentKit.Artifacts.InMemory.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Artifacts.InMemory](../../src/AgentKit.Artifacts.InMemory/README.md)
  — implementation and registration entry points.
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
