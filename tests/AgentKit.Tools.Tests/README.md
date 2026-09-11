# AgentKit.Tools.Tests

Focused tests for [AgentKit.Tools](../../src/AgentKit.Tools/README.md).

**Component purpose:** catalog, validate, authorize, and invoke application
tools.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

`ToolCatalogCaptureTests` runs the reusable catalog-capture suite and adds exact
source-graph validation, hostile metadata and lease responses, cleanup failure
aggregation, owned/borrowed DI scopes, and safe diagnostic/observer coverage.

`StaticToolProviderTests` runs reusable discovery conformance plus static
binding, DI scope, concurrent correlation, and observer-isolation checks.
Registration validation, typed keys, and replacement stay in
`ServiceExtensionsTests`; the shared binding graph has its own
`ToolProviderBindingsTests` fixture.

`RejectingToolCatalogMergePolicyTests` runs the reusable policy suite through
public DI and adds default rejection and safe-observation checks.
`ToolCatalogMergeGraphTests` covers complete deterministic collisions, missing
membership, hostile comparers, exact selection validation, and retained order.
`ToolCatalogMergerTests` composes static discovery, merge, catalog capture, and
invoker acquisition, including cancellation and failed policy decisions.
`ToolCatalogMergeObservationTests` covers diagnostic lifecycle and observer
failures. Policy cardinality and replacement remain in `ServiceExtensionsTests`.

`ToolRegistrationCatalogTests` runs reusable registration conformance through
public DI, adds composition guards and safe diagnostic coverage, and follows
old/new selections through discovery, merge, catalog capture, and exact invoker
acquisition after replacement. Provider ownership, typed keys, cardinality, and
registration replacement remain in `ServiceExtensionsTests`; internal marker and
registration-helper guards stay in their respective owning-class fixtures.

## Start with these tests

- [ToolProviderCaptureTests](ToolProviderCaptureTests.cs) — reusable
  source-capture conformance, DI scope ownership, concurrency, cleanup failure,
  and diagnostics.
- [ToolInvokerLeaseTests](ToolInvokerLeaseTests.cs) — exact binding lifetime and
  concurrent idempotent release.
- [AllowListToolAuthorizerTests](AllowListToolAuthorizerTests.cs)
- [DefaultToolInvokerTests](DefaultToolInvokerTests.cs)
- [ToolCatalogTests](ToolCatalogTests.cs)
- [ToolResultProjectionPolicyCatalogTests](ToolResultProjectionPolicyCatalogTests.cs)
- [ServiceExtensionsTests](ServiceExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Tools.Tests/AgentKit.Tools.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Tools](../../src/AgentKit.Tools/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/tools.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
