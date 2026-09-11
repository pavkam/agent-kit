# AgentKit.Budgets.Tests

Focused tests for [AgentKit.Budgets](../../src/AgentKit.Budgets/README.md).

**Component purpose:** reserve and account for capacity across hierarchical
budget scopes.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [BudgetAuthorityTests](BudgetAuthorityTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Budgets.Tests/AgentKit.Budgets.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Budgets](../../src/AgentKit.Budgets/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/budgets.md) — intended
  contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
