# WS19: Evaluation

Goal: an `AgentKit.Evaluation` application leaf (allowed to reference the
`AgentKit` facade) with versioned plans and cases, evaluators, result stores,
report exporters, a runner with concurrency, repetition, and deterministic
ordering, and InMemory, Sqlite, and Json result stores under one conformance
suite.

No evaluation project exists today. The architecture tests already permit
`AgentKit.Evaluation → AgentKit`
(`tests/AgentKit.Architecture.Tests/ProjectReferenceGraphTests.cs:88-97`).

Owning document:
[Testing and evaluation](../architecture/testing-and-evaluation.md).

## Progress

- [ ] WS19-C1 scaffold project and tests
- [ ] WS19-C2 identity and plan value types
- [ ] WS19-C3 evaluator, store, exporter contracts
- [ ] WS19-C4 runner, options, registration
- [ ] WS19-C5 deterministic evaluators
- [ ] WS19-C6 `ModelJudgeEvaluator`
- [ ] WS19-C7 result-store adapters and conformance
- [ ] WS19-C8 example and documentation

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

### WS19-C2: Identity and plan value types

- Depends on: C1. Size: S.
- Deliverables: `EvaluationRunId`, `EvaluationPlanId`, `EvaluationCaseId`,
  `EvaluatorKey`, `EvaluationResultStoreKey`, `EvaluationReportExporterKey`,
  `EvaluationPlanVersion`, `EvaluationPlan`, `EvaluationCase`,
  `EvaluationCaseExecution`, `EvaluationExecutionPolicy`,
  `EvaluationRecordingPolicy`, `EvaluatorReference`, `EvaluationCriteria`,
  `EvaluationFixtureReference`; run-id generator registration; guard tests.
  Write NO-SPEC bodies into the architecture document first.

### WS19-C3: Contracts

- Depends on: C2. Size: S.
- Deliverables: `IEvaluator`, `EvaluatorDescriptor`, `EvaluationContext`,
  `EvaluationOutcome` (seven variants), `IEvaluationResultStore`,
  `EvaluationStoreResult`, `IEvaluationResultStoreSelector`, selection,
  `IEvaluationReportExporter`, `EvaluationExportResult`, `EvaluationCaseResult`,
  `EvaluationReport`, `IEvaluatorCatalog`.

### WS19-C4: Runner, options, registration

- Depends on: C3, WS1 facade. Risk: facade shape dependency. Size: M.
- Deliverables: `EvaluationOptions` and snapshot, `EvaluationRunner`,
  `EvaluationExecution`, `AddAgentEvaluation`, `ReplaceEvaluationRunner`,
  `AddEvaluator`, `ReplaceEvaluator`, `AddEvaluationResultStore`,
  `AddEvaluationReportExporter`; session-profile pre-check; tests for
  concurrency, repetition, cancellation, exporter-failure isolation, profile
  mismatch failing before effects.

### WS19-C5: Deterministic evaluators

- Depends on: C4. Size: M.
- Deliverables: `SchemaEvaluator`, `ExactStateEvaluator`, `ToolEffectEvaluator`,
  `SafetyEvaluator` with fixture-driven tests.

### WS19-C6: `ModelJudgeEvaluator`

- Depends on: C5, WS7. Size: M.
- Deliverables: explicit model key, budget, rubric recording; recorded-fixture
  test.

### WS19-C7: Result-store adapters and conformance

- Depends on: C3. Size: M.
- Deliverables: `AgentKit.Evaluation.InMemory`, `.Sqlite`, `.Json`;
  `IEvaluationResultStoreConformanceFixture`; update
  `testing-and-evaluation.md:366-369` to include Json.

### WS19-C8: Example and documentation

- Depends on: C4–C7. Size: S.
- Deliverables: `examples/Evaluation`; architecture document, evaluation skill,
  `AGENTS.md` map, `docs/packages/index.md`.

## Totals

S 4, M 4. New projects: four source, four test, one conformance fixture, one
example.
