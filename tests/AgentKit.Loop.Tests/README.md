# AgentKit.Loop.Tests

Focused tests for [AgentKit.Loop](../../src/AgentKit.Loop/README.md).

**Component purpose:** coordinate turns, context preparation, model attempts,
tool calls, and terminal outcomes.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DefaultAgentLoopTests](DefaultAgentLoopTests.cs)
- [DefaultRunContinuationPolicyTests](DefaultRunContinuationPolicyTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Loop.Tests/AgentKit.Loop.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Loop](../../src/AgentKit.Loop/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/agent-runtime.md) — intended
  contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
