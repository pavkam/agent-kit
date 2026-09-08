# AgentKit.Hooks.Tests

Focused tests for [AgentKit.Hooks](../../src/AgentKit.Hooks/README.md).

**Component purpose:** dispatch typed lifecycle hooks with ordering, mutation
validation, and failure policy.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DefaultHookDispatcherTests](DefaultHookDispatcherTests.cs)
- [HookDispatchScopeTests](HookDispatchScopeTests.cs)
- [ObservabilityTests](ObservabilityTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Hooks.Tests/AgentKit.Hooks.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Hooks](../../src/AgentKit.Hooks/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/extensions.md) — intended
  contract and ownership.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
