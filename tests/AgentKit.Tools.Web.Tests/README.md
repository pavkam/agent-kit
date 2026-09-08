# AgentKit.Tools.Web.Tests

Focused tests for [AgentKit.Tools.Web](../../src/AgentKit.Tools.Web/README.md).

**Component purpose:** fetch web content through bounded network operations and
content projection.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [WebContentProjectorTests](WebContentProjectorTests.cs)
- [WebFetchToolTests](WebFetchToolTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Tools.Web.Tests/AgentKit.Tools.Web.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Tools.Web](../../src/AgentKit.Tools.Web/README.md) — implementation
  and registration entry points.
- [Component specification](../../docs/architecture/tools.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
