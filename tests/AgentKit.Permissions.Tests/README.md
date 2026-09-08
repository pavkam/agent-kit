# AgentKit.Permissions.Tests

Focused tests for
[AgentKit.Permissions](../../src/AgentKit.Permissions/README.md).

**Component purpose:** evaluate security requests and manage bounded grants,
profiles, and audit dispatch.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [DefaultSecurityAuditDispatcherConformanceTests](DefaultSecurityAuditDispatcherConformanceTests.cs)
- [DefaultSecurityAuditDispatcherTests](DefaultSecurityAuditDispatcherTests.cs)
- [DefaultSecurityAuthoritySelectorTests](DefaultSecurityAuthoritySelectorTests.cs)
- [DefaultSecurityProfilePublicationReaderTests](DefaultSecurityProfilePublicationReaderTests.cs)
- [DefaultSecurityProfileSelectorTests](DefaultSecurityProfileSelectorTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Permissions.Tests/AgentKit.Permissions.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md) —
  implementation and registration entry points.
- [Component specification](../../docs/architecture/permissions-and-human-control.md)
  — intended contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
