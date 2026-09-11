# AgentKit.Session.InMemory.Tests

Focused tests for
[AgentKit.Session.InMemory](../../src/AgentKit.Session.InMemory/README.md).

**Component purpose:** keep session records and directory state in memory for
ephemeral applications.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [InMemorySessionStoreTests](InMemorySessionStoreTests.cs)
- [DefaultSessionCoordinatorTests](DefaultSessionCoordinatorTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Session.InMemory.Tests/AgentKit.Session.InMemory.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Session.InMemory](../../src/AgentKit.Session.InMemory/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/sessions.md) — intended
  contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
