# CodingAgent.Tests

Focused tests for the [CodingAgent](../../examples/CodingAgent/README.md)
example.

**Component purpose:** verify the terminal coding-agent example: runtime
composition, slash commands, permission modes, and approval flow.

Use this suite when changing the example or investigating a regression. It is a
non-packable .NET 10 test project using xUnit v3 and Shouldly. It never calls a
live model: providers are replaced with loopback handlers or scripted fakes.

## Start with these tests

- [AgentConfigurationDialogTests](AgentConfigurationDialogTests.cs)
- [AgentRuntimeTests](AgentRuntimeTests.cs)
- [ChatScreenTests](ChatScreenTests.cs)
- [CodingAgentApprovalHandlerTests](CodingAgentApprovalHandlerTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/CodingAgent.Tests/CodingAgent.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [CodingAgent](../../examples/CodingAgent/README.md) — the example under test.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
