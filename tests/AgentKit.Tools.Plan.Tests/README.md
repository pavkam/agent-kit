# AgentKit.Tools.Plan.Tests

Focused tests for
[AgentKit.Tools.Plan](../../src/AgentKit.Tools.Plan/README.md).

**Component purpose:** maintain versioned planning state through session-backed
tool operations.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [PlanToolTests](PlanToolTests.cs)
- [SessionPlanStateStoreTests](SessionPlanStateStoreTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Tools.Plan.Tests/AgentKit.Tools.Plan.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Tools.Plan](../../src/AgentKit.Tools.Plan/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/tools.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
