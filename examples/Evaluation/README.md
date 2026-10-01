# Evaluation

A complete [AgentKit.Evaluation](../../src/AgentKit.Evaluation/README.md)
composition over the same one-model agent as
[QuickStart](../QuickStart/README.md): a two-case versioned plan judged by the
deterministic `ExactStateEvaluator` and `SafetyEvaluator`, results kept in an
in-memory result store, and a text report exporter.
[EvaluationExample.Tests](../../tests/EvaluationExample.Tests) covers it offline
against a stub HTTP handler, so it always compiles and runs.

## Run it

```sh
export OPENAI_API_KEY=sk-...
dotnet run --project examples/Evaluation
```

The process exits non-zero when any case fails.

## What it shows

- **One engine, many cases.** The runner is bound to the one built
  `AgentEngine`; each case names an `AgentId`, carries its authenticated
  identity and expected session profile, and runs in its own session.
- **Deterministic first.** Exact run outcome and a forbidden-content canary are
  checked by code. A `ModelJudgeEvaluator` (with an explicit judge model and its
  own budget) is available for criteria code cannot assess; register it with
  `AddModelJudgeEvaluator` and `AddModelRequestJudgeClient`.
- **Explicit destinations.** The plan names its result store and exporter by
  key. Swap the in-memory store for `AddSqliteEvaluationResultStore` or
  `AddJsonEvaluationResultStore` to keep results across restarts.
- **Honest results.** Reports distinguish evaluated, rejected, cancelled,
  timed-out, and faulted repetitions, and carry no prompts or model output.

Target: **.NET 10**.
