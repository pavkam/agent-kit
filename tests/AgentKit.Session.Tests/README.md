# AgentKit.Session.Tests

Focused tests for [AgentKit.Session](../../src/AgentKit.Session/README.md).

**Component purpose:** coordinate session lifecycle, run ownership, branching,
and store routing.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [ArgumentExceptionExtensionsTests](ArgumentExceptionExtensionsTests.cs)
- [DefaultSessionCoordinatorTests](DefaultSessionCoordinatorTests.cs)
- [DefaultSessionRunCoordinatorTests](DefaultSessionRunCoordinatorTests.cs)
- [DefaultSessionStoreSelectorTests](DefaultSessionStoreSelectorTests.cs)
- [InMemorySessionDirectoryTests](InMemorySessionDirectoryTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Diagnostic listeners

Observation and fault-injection callbacks filter by the test-owned trace and
operation. `ActivityListener.Sample` does not restrict callbacks when another
subscriber samples an activity. The lease tests exercise both another operation
and another lease trace while the throwing listener is installed, and verify
that the intended callback still throws. Metric fault injection uses the same
trace isolation so parallel tests retain their observations.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Session.Tests/AgentKit.Session.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Session](../../src/AgentKit.Session/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/sessions.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
