# WS19: Evaluation

Goal: an `AgentKit.Evaluation` application leaf (allowed to reference the
`AgentKit` facade) with versioned plans and cases, evaluators, result stores,
report exporters, a runner with concurrency, repetition, and deterministic
ordering, and InMemory, Sqlite, and Json result stores under one conformance
suite.

The architecture tests already permit `AgentKit.Evaluation → AgentKit`
(`tests/AgentKit.Architecture.Tests/ProjectReferenceGraphTests.cs:88-97`).

Owning document:
[Testing and evaluation](../architecture/testing-and-evaluation.md).

## Progress

- [x] WS19-C1 scaffold project and tests
- [x] WS19-C2 identity and plan value types
- [x] WS19-C3 evaluator, store, exporter contracts
- [x] WS19-C4 runner, options, registration
- [x] WS19-C5 deterministic evaluators
- [x] WS19-C6 `ModelJudgeEvaluator`
- [x] WS19-C7 result-store adapters and conformance
- [x] WS19-C8 example and documentation

## Verified current state

| Item                                                                                         | State                                                                                                                       | Evidence                                                                                                                 |
| -------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `AgentKit.Evaluation` project, snapshot, `AgentKit.slnx` entries                             | LANDED                                                                                                                      | `src/AgentKit.Evaluation`; `Snapshots/AgentKit.Evaluation*.verified.txt`; `AgentKit.slnx`                                |
| identities, plan, case, policies, criteria, fixture and evaluator references                 | LANDED, validated immutable values                                                                                          | `src/AgentKit.Evaluation/Evaluation*.cs`, `EvaluatorReference.cs`                                                        |
| `IEvaluator`, descriptor, context, seven-variant `EvaluationOutcome`, report, case result    | LANDED                                                                                                                      | `IEvaluator.cs`, `EvaluatorDescriptor.cs`, `EvaluationOutcome.cs`, `EvaluationCaseResult.cs`, `EvaluationReport.cs`      |
| `IEvaluationResultStore` (append and paged read), selector and selection, exporter, catalogs | LANDED                                                                                                                      | `IEvaluationResultStore.cs`, `IEvaluationResultStoreSelector.cs`, `IEvaluationReportExporter.cs`, `IEvaluatorCatalog.cs` |
| runner, options snapshot, registration                                                       | LANDED over the WS1 facade (`AgentRunResult<ValidatedOutput>`)                                                              | `EvaluationRunner.cs`, `EvaluationExecution.cs`, `EvaluationRegistration.cs`, `ServiceExtensions.cs`                     |
| deterministic evaluators                                                                     | LANDED: schema, exact-state, tool-effect, safety                                                                            | `SchemaEvaluator.cs`, `ExactStateEvaluator.cs`, `ToolEffectEvaluator.cs`, `SafetyEvaluator.cs`                           |
| `ModelJudgeEvaluator`                                                                        | LANDED with the `IModelJudgeClient` seam and first-party client                                                             | `ModelJudgeEvaluator.cs`, `ModelRequestJudgeClient.cs`                                                                   |
| InMemory, Sqlite, Json result stores and shared conformance suite                            | LANDED                                                                                                                      | `src/AgentKit.Evaluation.{InMemory,Sqlite,Json}`; `tests/AgentKit.Conformance/EvaluationResultStoreConformanceTests.cs`  |
| observability                                                                                | LANDED: `evaluation.*` activities, metrics, tags, log events 36000-36008 (runner), 36100-36102 (stores), 36300-36301 (Json) | `AgentKit.Observability` name classes; `EvaluationLog`, `EvaluationResultStoreLog`, `JsonEvaluationStoreLog`             |
| example                                                                                      | LANDED                                                                                                                      | `examples/Evaluation`; `tests/EvaluationExample.Tests`                                                                   |

## Spec coverage

| Contract                                                                                                                                                                                                                                                                                                              | Spec                                |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| identities                                                                                                                                                                                                                                                                                                            | `testing-and-evaluation.md:155-160` |
| `EvaluationPlan`, `EvaluationCaseExecution`, `EvaluationCase`                                                                                                                                                                                                                                                         | `:162-181`                          |
| `IEvaluationRunner`, `IEvaluator`, `IEvaluationResultStore`, `IEvaluationResultStoreSelector`, `IEvaluationReportExporter`                                                                                                                                                                                            | `:183-218`                          |
| runner internals; options; DI                                                                                                                                                                                                                                                                                         | `:252-279,310-347`                  |
| `EvaluationPlanVersion`, execution and recording policies, `EvaluatorReference`, `EvaluationCriteria`, `EvaluationFixtureReference`, `EvaluatorDescriptor`, `EvaluationContext`, `EvaluationOutcome` (variants in prose `:227-229`), store result, selection, export result, case result, report, `IEvaluatorCatalog` | NO-SPEC                             |

The runner injects `AgentEngine` (`:258`) and consumes the public result family;
C4 must follow WS1's facade so it targets `AgentRunResult<T>` rather than the
interim `AgentLoopResult`.

## Chunks

### WS19-C1: Scaffold

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `src/AgentKit.Evaluation` (`IsPackable=true`, `GlobalUsings`,
  `AssemblyInfo`, `ServiceExtensions`, README),
  `tests/AgentKit.Evaluation.Tests`, `AgentKit.slnx` entries, compatibility
  snapshot.
- Landed: the project references the `AgentKit` facade, `AgentKit.Abstractions`,
  and `AgentKit.Observability` only (the architecture test already permits it);
  shared evaluation activity, metric, and tag names were added to
  `AgentKit.Observability`; the `AgentKit.Evaluation*.verified.txt` snapshots
  and the solution entries landed (the later leaves and example in C7/C8).

### WS19-C2: Identity and plan value types

- Depends on: C1. Size: S.
- Deliverables: `EvaluationRunId`, `EvaluationPlanId`, `EvaluationCaseId`,
  `EvaluatorKey`, `EvaluationResultStoreKey`, `EvaluationReportExporterKey`,
  `EvaluationPlanVersion`, `EvaluationPlan`, `EvaluationCase`,
  `EvaluationCaseExecution`, `EvaluationExecutionPolicy`,
  `EvaluationRecordingPolicy`, `EvaluatorReference`, `EvaluationCriteria`,
  `EvaluationFixtureReference`; run-id generator registration; guard tests.
  Write NO-SPEC bodies into the architecture document first.
- Landed: the NO-SPEC shapes are recorded in "Implemented value shapes" in
  `testing-and-evaluation.md` first. Added `EvaluatorVersion`,
  `EvaluationCriterionKey`, and the abstract `EvaluationCriterion` so criteria
  are typed and extensible (a third-party package derives a sealed record).
  Every value validates in its constructor with `ArgumentException.ThrowIf*`;
  the run-id generator is a replaceable default registered by
  `AddAgentEvaluation`.

### WS19-C3: Contracts

- Depends on: C2. Size: S.
- Deliverables: `IEvaluator`, `EvaluatorDescriptor`, `EvaluationContext`,
  `EvaluationOutcome` (seven variants), `IEvaluationResultStore`,
  `EvaluationStoreResult`, `IEvaluationResultStoreSelector`, selection,
  `IEvaluationReportExporter`, `EvaluationExportResult`, `EvaluationCaseResult`,
  `EvaluationReport`, `IEvaluatorCatalog`.
- Landed: all listed contracts. Deviations recorded in the architecture
  document: `IEvaluationResultStore` also declares a paged `ReadAsync`
  (conformance and comparison need a public read seam); `EvaluationOutcome`
  variants carry safe evidence; the runner takes
  `IEvaluationReportExporterCatalog` instead of
  `IEnumerable<IEvaluationReportExporter>` so exporters resolve by key.

### WS19-C4: Runner, options, registration

- Depends on: C3, WS1 facade. Risk: facade shape dependency. Size: M.
- Deliverables: `EvaluationOptions` and snapshot, `EvaluationRunner`,
  `EvaluationExecution`, `AddAgentEvaluation`, `ReplaceEvaluationRunner`,
  `AddEvaluator`, `ReplaceEvaluator`, `AddEvaluationResultStore`,
  `AddEvaluationReportExporter`; session-profile pre-check; tests for
  concurrency, repetition, cancellation, exporter-failure isolation, profile
  mismatch failing before effects.
- Landed: `AddAgentEvaluation` also takes a runner by `TryAdd`, validates
  options and snapshots them, and binds exactly one `AgentEngine`; keyed
  registrations are idempotent for identical repeats and fail for key reuse.
  `RunAsync` validates the whole plan before effects
  (`EvaluationPlanRejectedException`), schedules a bounded worker pool in plan
  order, applies case timeout and plan deadline from the injected
  `TimeProvider`, isolates evaluator, store, and exporter failures, and returns
  a partial report on cancellation. Tests drive a real engine over the scripted
  `GatedAgentLoop` and cover concurrency bounds, deterministic ordering under
  reversed completion, repetition, cancellation, timeout, deadline,
  stop-on-failure, exporter isolation, profile mismatch before effects, and
  content-free signals. `GatedAgentLoop` gained reply, usage, and entry hooks.

### WS19-C5: Deterministic evaluators

- Depends on: C4. Size: M.
- Deliverables: `SchemaEvaluator`, `ExactStateEvaluator`, `ToolEffectEvaluator`,
  `SafetyEvaluator` with fixture-driven tests.
- Landed: each evaluator has a documented key, version 1, and criterion type
  (`SchemaCriterion`, `ExactStateCriterion`, `ToolEffectCriterion`,
  `SafetyCriterion`). Safety patterns are non-backtracking under a one-second
  limit; evidence never contains matched or expected content. Theory-driven
  fixtures cover valid and invalid outputs, comparisons, call counts, argument
  subsets, canonical-identity matching, and rejected or missing criteria.

### WS19-C6: `ModelJudgeEvaluator`

- Depends on: C5, WS7. Size: M.
- Deliverables: explicit model key, budget, rubric recording; recorded-fixture
  test.
- Landed: `ModelJudgeEvaluator` with `RubricCriterion`, `ModelJudgeSettings`,
  `ModelJudgeBudget`, the `IModelJudgeClient` seam, and the first-party
  `ModelRequestJudgeClient` over the provider-neutral model catalog, selector,
  and adapter. Recorded-fixture tests replay judge replies offline. Deviations
  (evaluator-local budget accounting, adapter-owned egress authority, no blinded
  order for single-candidate rubrics) are recorded in the architecture document.

### WS19-C7: Result-store adapters and conformance

- Depends on: C3. Size: M.
- Deliverables: `AgentKit.Evaluation.InMemory`, `.Sqlite`, `.Json`;
  `IEvaluationResultStoreConformanceFixture`; update
  `testing-and-evaluation.md:366-369` to include Json.
- Landed: the three leaves share the `AgentKit.Evaluation.Storage.Shared`
  planner and the `.Storage.Durable` documents through linked source, and each
  registers keyed through `Add{InMemory,Sqlite,Json}EvaluationResultStore`. The
  shared `EvaluationResultStoreConformanceTests` (identity, idempotency, run
  pinning, ordering, paging, concurrency, cancellation, reopen) runs in all
  three test projects; SQLite adds schema, identity, lock-contention, and
  cross-process atomicity tests, JSON adds torn-append recovery, conflicting-log
  refusal, encoding-contract binding, and single-writer rejection. The
  architecture and concept documents now name all three leaves, settling the
  conflict.

### WS19-C8: Example and documentation

- Depends on: C4–C7. Size: S.
- Deliverables: `examples/Evaluation`; architecture document, evaluation skill,
  `AGENTS.md` map, `docs/packages/index.md`.
- Landed: `examples/Evaluation` (two cases, deterministic evaluators, in-memory
  store, text exporter) with `tests/EvaluationExample.Tests` over a stub HTTP
  handler; the package READMEs, architecture document, evaluation skill,
  `AGENTS.md`, `docs/packages/index.md`, `project-structure.md`, and the
  use-cases index are updated.

## Totals

S 4, M 4. New projects: four source, four test, one conformance fixture, one
example (landed as four source, four test, two linked-source directories, one
conformance fixture and suite, one example, and one example test project).
