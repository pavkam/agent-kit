# AgentKit.Durability.Tests

Focused tests for
[AgentKit.Durability](../../src/AgentKit.Durability/README.md).

**Component purpose:** coordinate durable execution: profiles, recovery policy,
fenced journals, and the execution coordinator.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DefaultDurabilityRuntimeSelectorTests](DefaultDurabilityRuntimeSelectorTests.cs)
- [DefaultDurableBackendSelectorTests](DefaultDurableBackendSelectorTests.cs)
- [DefaultDurableExecutionEventDispatcherTests](DefaultDurableExecutionEventDispatcherTests.cs)
- [DefaultRecoveryPolicyTests](DefaultRecoveryPolicyTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Durability.Tests/AgentKit.Durability.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Durability](../../src/AgentKit.Durability/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/durable-execution.md) —
  intended contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
