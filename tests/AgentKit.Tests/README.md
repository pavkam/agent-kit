# AgentKit.Tests

Focused tests for [AgentKit](../../src/AgentKit/README.md).

**Component purpose:** compose a process-level engine that hosts immutable agent
definitions and isolated runs.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [AgentCompositionBuildObservabilityTests](AgentCompositionBuildObservabilityTests.cs)
- [AgentEngineBuilderTests](AgentEngineBuilderTests.cs)
- [AgentEngineTests](AgentEngineTests.cs)
- [AgentKitServiceProviderFactoryTests](AgentKitServiceProviderFactoryTests.cs)
- [AgentRunProfileCompositionTests](AgentRunProfileCompositionTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Tests/AgentKit.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit](../../src/AgentKit/README.md) — implementation and registration
  entry points.
- [Component specification](../../docs/architecture/composition-and-configuration.md)
  — intended contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
