# AgentKit.Evaluation.Tests

Focused tests for
[AgentKit.Evaluation](../../src/AgentKit.Evaluation/README.md).

**Component purpose:** run versioned evaluation plans through the public engine
and record honest, deterministic results.

Use this suite when changing a plan or result value, the runner, an evaluator,
or registration. It is a non-packable .NET 10 test project using xUnit v3 and
Shouldly.

## Start with these tests

- [EvaluationRunnerTests](EvaluationRunnerTests.cs) — concurrency, repetition,
  deterministic ordering, cancellation, exporter isolation, and plan validation
  that fails before effects, over a real engine with a scripted loop.
- [EvaluationPlanTests](EvaluationPlanTests.cs) and
  [EvaluationCaseResultTests](EvaluationCaseResultTests.cs) — the validated
  value shapes every other component consumes.
- [ServiceExtensionsTests](ServiceExtensionsTests.cs) — keyed registration,
  idempotency, and conflict detection.

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```bash
dotnet test --project tests/AgentKit.Evaluation.Tests -c Release
```
