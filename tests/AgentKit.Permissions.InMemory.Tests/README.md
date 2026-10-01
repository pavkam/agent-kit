# AgentKit.Permissions.InMemory.Tests

Focused tests for
[AgentKit.Permissions.InMemory](../../src/AgentKit.Permissions.InMemory/README.md).

**Component purpose:** keep grant, approval, and decision state in memory for
ephemeral applications.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [InMemoryApprovalStoreTests](InMemoryApprovalStoreTests.cs)
- [InMemorySecurityDecisionStoreConformanceTests](InMemorySecurityDecisionStoreConformanceTests.cs)
- [InMemorySecurityDecisionStoreTests](InMemorySecurityDecisionStoreTests.cs)
- [InMemorySecurityGrantStoreTests](InMemorySecurityGrantStoreTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Permissions.InMemory.Tests/AgentKit.Permissions.InMemory.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Permissions.InMemory](../../src/AgentKit.Permissions.InMemory/README.md)
  — implementation and registration entry points.
- [Component specification](../../docs/architecture/permissions-and-human-control.md)
  — intended contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
