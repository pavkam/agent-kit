# AgentKit.Evaluation

Versioned agent evaluation for AgentKit: plans and cases, evaluators, result
stores, report exporters, and a runner that drives a built `AgentEngine` through
its public surface.

Evaluation is an optional application leaf. It consumes the `AgentKit` facade
and receives no loop, run scope, provider SDK, or raw service provider. Contract
conformance proves a component preserves a public boundary; evaluation measures
a composed agent against a dataset and never decides whether ordering, security
enforcement, hook behavior, or persistence is correct.

```csharp
services.AddAgentEvaluation(options => options.MaximumConcurrentCases = 4);
services.AddEvaluator<ExactStateEvaluator>(ExactStateEvaluator.Key);
services.AddEvaluator<SafetyEvaluator>(SafetyEvaluator.Key);
services.AddInMemoryEvaluationResultStore(new EvaluationResultStoreKey("memory"));   // or Sqlite / Json

var report = await runner.RunAsync(plan, cancellationToken);
```

## Plans and cases

An `EvaluationPlan` has a stable `EvaluationPlanId`, an `EvaluationPlanVersion`,
ordered `EvaluationCase`s, an `EvaluationExecutionPolicy` (concurrency,
repetitions, case timeout, plan deadline, run cap, stop-on-failure), and an
`EvaluationRecordingPolicy` naming one result store and the exporters by key. A
case names an `AgentId`, carries its already-authenticated `ExecutionIdentity`
and expected `SessionProfileKey`, the input, run overrides, evaluator
references, typed `EvaluationCriteria`, and an optional fixture reference.

`RunAsync` validates the whole plan before any effect (agent hosted, session
profile matches the definition, evaluators and destinations registered, limits
respected) and throws `EvaluationPlanRejectedException` listing every problem.
Results are ordered by plan case position then repetition regardless of parallel
execution. Cancellation returns a truthful partial report and never deletes
persisted results.

## Evaluators

| Key           | Evaluator             | Criterion             | Checks                                                        |
| ------------- | --------------------- | --------------------- | ------------------------------------------------------------- |
| `schema`      | `SchemaEvaluator`     | `SchemaCriterion`     | Output against a JSON schema through `IOutputSchemaEngine`    |
| `exact-state` | `ExactStateEvaluator` | `ExactStateCriterion` | Run outcome, text, JSON, new-message count                    |
| `tool-effect` | `ToolEffectEvaluator` | `ToolEffectCriterion` | Required and forbidden tool calls, counts, argument subsets   |
| `safety`      | `SafetyEvaluator`     | `SafetyCriterion`     | Forbidden substrings and patterns, required refusal markers   |
| `model-judge` | `ModelJudgeEvaluator` | `RubricCriterion`     | Semantic rubric judged by an explicit model, with uncertainty |

Prefer the deterministic evaluators. The judge needs an explicit judge model
alias and its own bounded budget (`AddModelJudgeEvaluator`,
`AddModelRequestJudgeClient`), repeats each judgement, reports the sample
standard deviation, and records the rubric, models, and a prompt fingerprint.
Outcomes distinguish passed, failed, inconclusive, skipped, cancelled,
unsupported, and evaluator failure.

## Results

A recorded `EvaluationCaseResult` is a bounded projection: typed run and session
identity, trace identity, agent and model manifest, usage (unknown stays
unknown), latency, fixture reference, evaluator outcomes with version and safe
evidence, and diagnostics. Prompts, model output, and tool data are never
recorded. Store adapters: `AgentKit.Evaluation.InMemory`, `.Sqlite`, and
`.Json`; exporters are separate keyed effects whose failure never changes a
result.
