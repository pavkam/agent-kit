# AgentKit.Test.Shared

Shared fixtures for AgentKit test projects: deterministic execution identity,
security evidence, and activity observation. Keep common test mechanics here so
component suites can focus on the behavior they exercise.

This is a non-packable support library. It is consumed by test projects and has
no standalone test run or production registration API.

## Available helpers

- [UninvokedSecurityAuthority](UninvokedSecurityAuthority.cs) — detects
  unexpected authorization during binding, construction, or selection tests.

- [RunResultTestData](RunResultTestData.cs) — deterministic final-result,
  deferral and correlated stream fixtures.
- [TestExecutionIdentity](TestExecutionIdentity.cs) — execution identity
  fixtures.
- [TestSecurityEvidence](TestSecurityEvidence.cs) — security evidence fixtures.
- [ActivityCollector](ActivityCollector.cs) — activity observation support.
- [ActivityObservation](ActivityObservation.cs) — captured observation values.
- [RecordingLogger](RecordingLogger.cs) and
  [RecordingLogEntry](RecordingLogEntry.cs) — structured log capture and
  deliberate observer failure.
- [ToolProjectionPolicyTestData](ToolProjectionPolicyTestData.cs) — exact policy
  revisions shared by value, runtime, and conformance tests.
- [ToolCatalogMergeTestData](ToolCatalogMergeTestData.cs) — typed toolset,
  source, alias, and candidate publications with coherent discovery requests.
- [CallbackToolCatalogMergePolicy](CallbackToolCatalogMergePolicy.cs) — a typed
  policy callback for explicit selection, rejection, cancellation, and hostile
  decision tests.

Add a helper when multiple test projects need it. Reusable contract assertions
belong in AgentKit.Conformance; production packages must not depend on either
project.

## Verify changes

Build the support library from the repository root, then run the suites that
consume the changed helper:

```sh
dotnet build tests/AgentKit.Test.Shared/AgentKit.Test.Shared.csproj --configuration Release
```

## Related projects

- [AgentKit.Conformance](../AgentKit.Conformance/README.md) — reusable
  behavioral suites.
- [AgentKit.Observability](../../src/AgentKit.Observability/README.md) — shared
  diagnostics conventions.
- [AgentKit.Identity](../../src/AgentKit.Identity/README.md) — production
  identity behavior.
- [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md) — production
  security behavior.
- [Testing guide](../../docs/testing/index.md) — evidence layers and commands.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
