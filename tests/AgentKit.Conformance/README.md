# AgentKit.Conformance

Reusable behavioral suites for interchangeable AgentKit implementations. Add a
fixture for an adapter and run the same contract cases in its owning test
project, so replacing an implementation preserves observable behavior.

This is non-packable test infrastructure. Abstract suites are exercised through
concrete adapter fixtures; building this project alone is not a conformance run.

## Available suites

- [ArtifactStoreConformanceTests](ArtifactStoreConformanceTests.cs)
- [IdentityNormalizerConformanceTests](IdentityNormalizerConformanceTests.cs)
- [InputPromotionPolicyConformanceTests](InputPromotionPolicyConformanceTests.cs)
- [OutputSchemaEngineConformanceTests](OutputSchemaEngineConformanceTests.cs)
- [RunContinuationPolicyConformanceTests](RunContinuationPolicyConformanceTests.cs)
- [SecurityAuditDispatcherConformanceTests](SecurityAuditDispatcherConformanceTests.cs)
- [SecurityGrantStoreConformanceTests](SecurityGrantStoreConformanceTests.cs)
- [SessionRunCoordinatorConformanceTests](SessionRunCoordinatorConformanceTests.cs)
- [SessionStoreConformanceTests](SessionStoreConformanceTests.cs)

Fixture interfaces define how each adapter supplies the services and scenarios
needed by its suite. Keep reusable behavior here and implementation-specific
regressions in the owning test project.

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
