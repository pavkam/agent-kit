# AgentKit.Permissions.Json.Tests

Focused tests for
[AgentKit.Permissions.Json](../../src/AgentKit.Permissions.Json/README.md).

**Component purpose:** store security state in inspectable single-writer JSON
files.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [JsonApprovalLogRecordTests](JsonApprovalLogRecordTests.cs)
- [JsonApprovalRequestTests](JsonApprovalRequestTests.cs)
- [JsonApprovalResponseTests](JsonApprovalResponseTests.cs)
- [JsonApprovalScopeBindingTests](JsonApprovalScopeBindingTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Permissions.Json.Tests/AgentKit.Permissions.Json.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Permissions.Json](../../src/AgentKit.Permissions.Json/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/permissions-and-human-control.md)
  — intended contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
