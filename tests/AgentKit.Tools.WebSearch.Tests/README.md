# AgentKit.Tools.WebSearch.Tests

Focused tests for
[AgentKit.Tools.WebSearch](../../src/AgentKit.Tools.WebSearch/README.md).

**Component purpose:** search the web through an explicitly configured search
provider.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [WebSearchToolTests](WebSearchToolTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Tools.WebSearch.Tests/AgentKit.Tools.WebSearch.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Tools.WebSearch](../../src/AgentKit.Tools.WebSearch/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/tools.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
