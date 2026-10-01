# AgentKit.Session.Json.Tests

Focused tests for
[AgentKit.Session.Json](../../src/AgentKit.Session.Json/README.md).

**Component purpose:** store sessions and the session directory in inspectable
single-writer JSON files.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [CreationRouteKeyTests](CreationRouteKeyTests.cs)
- [CreationRouteTests](CreationRouteTests.cs)
- [DirectoryAccessAllowedTests](DirectoryAccessAllowedTests.cs)
- [DirectoryAccessDeniedTests](DirectoryAccessDeniedTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Session.Json.Tests/AgentKit.Session.Json.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Session.Json](../../src/AgentKit.Session.Json/README.md) —
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
