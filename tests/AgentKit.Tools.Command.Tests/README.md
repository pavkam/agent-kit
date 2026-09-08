# AgentKit.Tools.Command.Tests

Focused tests for
[AgentKit.Tools.Command](../../src/AgentKit.Tools.Command/README.md).

**Component purpose:** offer an explicit shell-command tool over the process
boundary.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [CommandToolTests](CommandToolTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Tools.Command.Tests/AgentKit.Tools.Command.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Tools.Command](../../src/AgentKit.Tools.Command/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/tools.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
