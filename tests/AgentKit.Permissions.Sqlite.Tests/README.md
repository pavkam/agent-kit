# AgentKit.Permissions.Sqlite.Tests

Focused tests for
[AgentKit.Permissions.Sqlite](../../src/AgentKit.Permissions.Sqlite/README.md).

**Component purpose:** store security grants, decisions, and approvals in
durable local SQLite.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [ArgumentExceptionExtensionsTests](ArgumentExceptionExtensionsTests.cs)
- [SecurityGrantStoreUnavailableExceptionTests](SecurityGrantStoreUnavailableExceptionTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)
- [SqliteApprovalStoreTests](SqliteApprovalStoreTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Permissions.Sqlite.Tests/AgentKit.Permissions.Sqlite.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Permissions.Sqlite](../../src/AgentKit.Permissions.Sqlite/README.md)
  — implementation and registration entry points.
- [Component specification](../../docs/architecture/permissions-and-human-control.md)
  — intended contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
