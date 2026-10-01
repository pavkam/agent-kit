# AgentKit.Observability.OpenTelemetry.Tests

Focused tests for
[AgentKit.Observability.OpenTelemetry](../../src/AgentKit.Observability.OpenTelemetry/README.md).

**Component purpose:** bridge run events and security audit records to
`System.Diagnostics` without an exporter SDK.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [OpenTelemetryContentCaptureTests](OpenTelemetryContentCaptureTests.cs)
- [OpenTelemetryObservationOptionsSnapshotTests](OpenTelemetryObservationOptionsSnapshotTests.cs)
- [OpenTelemetryObservationRegistrationTests](OpenTelemetryObservationRegistrationTests.cs)
- [OpenTelemetryRunEventSinkTests](OpenTelemetryRunEventSinkTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.Observability.OpenTelemetry.Tests/AgentKit.Observability.OpenTelemetry.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.Observability.OpenTelemetry](../../src/AgentKit.Observability.OpenTelemetry/README.md)
  — implementation and registration entry points.
- [Component specification](../../docs/architecture/observability.md) — intended
  contract and ownership.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
