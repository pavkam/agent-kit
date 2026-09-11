# AgentKit.Identity.Tests

Focused tests for [AgentKit.Identity](../../src/AgentKit.Identity/README.md).

**Component purpose:** normalize trusted ingress identity and derive constrained
delegated identities.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [ExecutionIdentityResolverTests](ExecutionIdentityResolverTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Identity.Tests/AgentKit.Identity.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Identity](../../src/AgentKit.Identity/README.md) — implementation
  and registration entry points.
- [Component specification](../../docs/architecture/identity.md) — intended
  contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
