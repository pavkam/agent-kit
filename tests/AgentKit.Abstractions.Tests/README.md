# AgentKit.Abstractions.Tests

Focused tests for
[AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md).

**Component purpose:** implement AgentKit extensions against provider-neutral
contracts and typed domain values.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [AgentRunFinishedTests](Results/AgentRunFinishedTests.cs)
- [AgentRunOutcomeTests](Results/AgentRunOutcomeTests.cs)
- [DeferredOperationRequestTests](Results/DeferredOperationRequestTests.cs)
- [RunUsageTests](Usage/RunUsageTests.cs)
- [UsageContractTests](Usage/UsageContractTests.cs)
- [AgentHookEventArgsTests](Hooks/AgentHookEventArgsTests.cs)
- [AgentMessageHierarchyTests](Messages/AgentMessageHierarchyTests.cs)
- [AgentRunProfilePublicationContractsTests](Composition/AgentRunProfilePublicationContractsTests.cs)
- [ArgumentExceptionExtensionsTests](ArgumentExceptionExtensionsTests.cs)
- [ArgumentExceptionExtensionsTests](Budgets/ArgumentExceptionExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Abstractions.Tests/AgentKit.Abstractions.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/foundation-contracts.md) —
  intended contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
