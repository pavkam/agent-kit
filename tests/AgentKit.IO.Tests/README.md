# AgentKit.IO.Tests

Focused tests for [AgentKit.IO](../../src/AgentKit.IO/README.md).

**Component purpose:** provide input-promotion policy and a broker for bounded
human questions, plus bounded run-event fan-out.

Use this suite when changing that component or investigating a regression. It is
a non-packable .NET 10 test project using xUnit v3 and Shouldly.

## Start with these tests

- [RunEventStreamTests](RunEventStreamTests.cs)
- [RunEventHubTests](RunEventHubTests.cs)
- [RunEventHubObservationTests](RunEventHubObservationTests.cs)
- [RunEventSubscriptionTests](RunEventSubscriptionTests.cs)
- [RunEventHubOptionsTests](RunEventHubOptionsTests.cs)
- [RunEventHubMetricsTests](RunEventHubMetricsTests.cs)
- [DefaultHumanQuestionBrokerTests](DefaultHumanQuestionBrokerTests.cs)
- [DefaultInputPromotionPolicyConformanceTests](DefaultInputPromotionPolicyConformanceTests.cs)
- [DefaultInputPromotionPolicyTests](DefaultInputPromotionPolicyTests.cs)
- [InputPromotionObservabilityTests](InputPromotionObservabilityTests.cs)
- [InputPromotionPlanOutcomeExtensionsTests](InputPromotionPlanOutcomeExtensionsTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/AgentKit.IO.Tests/AgentKit.IO.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [AgentKit.IO](../../src/AgentKit.IO/README.md) — implementation and
  registration entry points.
- [Component specification](../../docs/architecture/input-and-output.md) —
  intended contract and ownership.
- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — shared behavioral
  suites.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
