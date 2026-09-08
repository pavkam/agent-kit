# AgentKit.Context.Tests

Focused tests for [AgentKit.Context](../../src/AgentKit.Context/README.md).

**Component purpose:** assemble provider-ready context while preserving message
trust and tool-call correlation.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DefaultContextAssemblerTests](DefaultContextAssemblerTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Context.Tests/AgentKit.Context.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Context](../../src/AgentKit.Context/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/context.md) — intended
  contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
