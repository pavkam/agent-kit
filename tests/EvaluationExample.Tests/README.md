# EvaluationExample.Tests

Focused tests for the [Evaluation example](../../examples/Evaluation/README.md)
example.

**Component purpose:** verify the evaluation example: a plan, deterministic
evaluators, and a report over a scripted agent.

Use this suite when changing the example or investigating a regression. It is a
non-packable .NET 10 test project using xUnit v3 and Shouldly. It never calls a
live model: providers are replaced with loopback handlers or scripted fakes.

## Start with these tests

- [EvaluationExampleSetupTests](EvaluationExampleSetupTests.cs)

These are entry points into the suite, not a claim of complete architectural
conformance.

## Run this project

From the repository root:

```sh
dotnet test --project tests/EvaluationExample.Tests/EvaluationExample.Tests.csproj --configuration Release --timeout 300s
```

## Related projects and documentation

- [Evaluation example](../../examples/Evaluation/README.md) — the example under
  test.
- [AgentKit.Test.Shared](../AgentKit.Test.Shared/README.md) — shared test
  fixtures and observation helpers.
- [Testing guide](../../docs/testing/index.md) — focused runs, coverage, and API
  compatibility.

[Project catalog](../../docs/packages/index.md) ·
[Contributing](../../CONTRIBUTING.md)
