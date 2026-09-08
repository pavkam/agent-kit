# AgentKit.Observability.Tests

Focused tests for
[AgentKit.Observability](../../src/AgentKit.Observability/README.md).

**Component purpose:** share AgentKit logging, activity, metric, and tag
conventions across components.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [AgentKitActivityScopeTests](AgentKitActivityScopeTests.cs)
- [AgentKitDiagnosticsTests](AgentKitDiagnosticsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Observability.Tests/AgentKit.Observability.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Observability](../../src/AgentKit.Observability/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/observability.md) — intended
  contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
