# AgentKit.Conformance

Reusable behavioral suites for interchangeable AgentKit implementations. Add a
fixture for an adapter and run the same contract cases in its owning test
project, so replacing an implementation preserves observable behavior.

This is non-packable test infrastructure. Abstract suites are exercised through
concrete adapter fixtures; building this project alone is not a conformance run.

`ToolCatalogCaptureConformanceTests` exercises selected-source binding, pending
acquisition/closure races, independent leases, cancellation, and exact ownership
through a typed factory returning `IToolCatalogCapture`.

`ToolProviderConformanceTests` covers stable source identity, exact retained
bindings, empty publications, independent repeated/concurrent captures, and
pre-transfer cancellation through a typed provider factory. The static-provider
fixture resolves the subject through public DI registration and owns its hosts.

`ToolCatalogMergePolicyConformanceTests` exercises policies configured to accept
unambiguous graphs and reject unconfigured collisions. It checks exact
contributions, explicit aliases, empty catalogs, all closed collision cases,
cancellation, and concurrent independent requests through a typed factory.

`ToolRegistrationCatalogConformanceTests` resolves subjects through a typed
factory. It checks exact authored order and policy versions, shared sources,
complete rejection, empty selections, cancellation, concurrent request
isolation, and absence of discovery or live provider metadata reads.

## Available suites

- [AgentRunStreamConformance](AgentRunStreamConformance.cs) — subscription
  cancellation, disposal, single-reader ownership and independent completion.
- [ArtifactStoreConformanceTests](ArtifactStoreConformanceTests.cs)
- [IdentityNormalizerConformanceTests](IdentityNormalizerConformanceTests.cs)
- [InputPromotionPolicyConformanceTests](InputPromotionPolicyConformanceTests.cs)
- [OutputSchemaEngineConformanceTests](OutputSchemaEngineConformanceTests.cs)
- [RunContinuationPolicyConformanceTests](RunContinuationPolicyConformanceTests.cs)
- [SecurityAuditDispatcherConformanceTests](SecurityAuditDispatcherConformanceTests.cs)
- [SecurityGrantStoreConformanceTests](SecurityGrantStoreConformanceTests.cs)
- [SessionRunCoordinatorConformanceTests](SessionRunCoordinatorConformanceTests.cs)
- [SessionStoreConformanceTests](SessionStoreConformanceTests.cs)
- [ToolResultProjectionPolicyCatalogConformanceTests](ToolResultProjectionPolicyCatalogConformanceTests.cs)
- [ToolProviderCaptureConformanceTests](ToolProviderCaptureConformanceTests.cs)
- [ToolInvokerLeaseConformanceTests](ToolInvokerLeaseConformanceTests.cs)

Fixture interfaces define how each adapter supplies the services and scenarios
needed by its suite. Keep reusable behavior here and implementation-specific
regressions in the owning `<ProductionClass>Tests` fixture.

The run-stream suite currently exercises the public stream contract through the
internal I/O adapter. Complete publisher registration and DI conformance remain
open until durable publication is integrated.

The value suites use typed constructors and accessors supplied by each concrete
fixture, without discovering members through reflection:

- [GuidIdentityConformanceTests](GuidIdentityConformanceTests.cs)
- [StringIdentityConformanceTests](StringIdentityConformanceTests.cs)
- [LongIdentityConformanceTests](LongIdentityConformanceTests.cs)
- [SingleMessageLeafConformanceTests](SingleMessageLeafConformanceTests.cs)

## Run a concrete implementation

For example, from the repository root:

```sh
dotnet test --project tests/AgentKit.Session.InMemory.Tests/AgentKit.Session.InMemory.Tests.csproj --configuration Release --timeout 300s
```

## Related projects

- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — reusable test
  support.
- [AgentKit.Session.InMemory.Tests](../AgentKit.Session.InMemory.Tests/README.md)
  — session adapter conformance.
- [AgentKit.Identity.Tests](../AgentKit.Identity.Tests/README.md) — identity
  conformance.
- [AgentKit.Compatibility.Tests](../AgentKit.Compatibility.Tests/README.md) —
  compiled API shape checks.
- [Testing guide](../../docs/testing/index.md) — how the evidence layers fit
  together.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
