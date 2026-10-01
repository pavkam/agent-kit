# Testing and evaluation

**Role:** Prove interchangeable components behave correctly and measure whether
complete agents still do useful work.

[Contract tests and agent evaluations answer different questions](../concepts/testing-and-evaluation.md).
Conformance proves that an implementation preserves a public boundary.
Evaluation measures the behavior of a composed agent against a versioned dataset
and rubric. A good score cannot excuse broken ordering, hook isolation, security
enforcement, cancellation, or persistence.

## Repository test structure

Every source project has a matching xUnit v3 project under tests. The matching
project owns implementation-specific unit, protocol, options, registration, and
disposal tests. AgentKit.Test.Shared contains deterministic fakes and fixtures;
AgentKit.Conformance contains reusable behavioral suites; and
AgentKit.Compatibility.Tests snapshots every packable public API.

Concrete fixtures are named `<ProductionClass>Tests` and contain that class's
argument checks, behavioral cases, diagnostics, and regressions. Merge these
cases into the existing fixture instead of creating scenario or invariant
fixtures. Concrete fixtures inherit applicable abstract conformance suites;
shared value suites use typed factories and accessors supplied by the owning
fixture. Behavioral construction and assertions do not discover constructors or
properties through reflection. Reflection is appropriate when reflection or API
metadata is itself the contract being tested.

Provider packages that reuse OpenAICompatible run both the shared protocol
family suite and their concrete provider suite. The latter verifies the
[provider-request contract](../concepts/provider-request-pipeline.md), including
capability claims, credentials, defaults, errors, options, and service
registration that the wire-family package cannot know.

All time-dependent tests replace TimeProvider. IDs, randomness, transports,
queues, hook order, and scheduling gates are controllable. Unit and conformance
tests never require live credentials, public network access, real processes, or
the developer's real filesystem. They use the in-memory file-system and network
packages plus scripted processes.

Composition tests validate both dependency graphs. The project gate rejects a
behavioral runtime-to-runtime or runtime-to-integration reference that violates
the ranked package DAG. It explicitly permits shared diagnostic infrastructure
and application leaves driving the facade. AgentKit.Tests builds closed service
graphs containing direct, keyed, additive, optional, factory-created, and
mixed-lifetime registrations. It proves that every declared cycle reports its
complete path. Undeclared factory dependencies are rejected rather than treated
as verified; `Lazy<T>`, `Func<T>`, or nested scopes do not excuse a declared
reverse edge. Tests cannot prove what arbitrary executable callbacks will do;
review and conformance enforce their declared boundaries.

Storage conformance is adapter-neutral. Every `.InMemory`, `.Sqlite`, and
`.Json` leaf for one contract runs the same common suite through its public DI
registration. SQLite-specific cases use an isolated temporary target and cover
close/reopen, schema migration, concurrent connections, rollback at transaction
cuts, and declared durability. In-memory cases prove ephemerality. Neither suite
may skip a common operation or infer distributed fencing, cross-store
transactions, or external-effect atomicity from local database durability.

Regression fixtures cover the dangerous seams explicitly: session/security
stores never call session coordination, output processors never call provider
execution, compaction summary generators never call context assembly, artifact
stores never append session/tool records, identity resolvers never depend on
downstream authorization, and observation sinks never become control
dependencies.

## Cross-component acceptance matrix

These scenarios are contract gates. A unit test of either happy-path component
alone is insufficient; each fixture composes deterministic alternatives across
the named boundary and injects failure at the commit or ownership transition.

| Boundary                            | Required evidence                                                                                                                                          |
| ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Definition reload and activation    | Conflicting same-revision content is rejected; a removed definition cannot run through an old handle; retained open work can reconcile                     |
| Build and external readiness        | Synchronous build performs no network or secret access; unavailable runtime services fail at their declared operation boundary                             |
| Lane admission and promotion        | Cross-lane inputs never mix; idempotent replay succeeds when the queue is full; promotion and history commit once                                          |
| Session idempotency and concurrency | A committed append replay returns its original receipt before version-conflict evaluation; stale operations cannot commit to successor branches            |
| Cancellation and settlement         | Cancelling one waiter leaves accepted work active; required writes use independent bounded cancellation; recovery-required is distinct from clean success  |
| Event publication and recovery      | New drives allocate above reserved sequence ranges; redelivery keeps event identity; lost live ranges require resnapshot                                   |
| Policy and enforcement              | All-abstain denies, explicit allow survives fallback policy, hard deny dominates, and exactly one leaf consumes each use                                   |
| Control-plane bootstrap             | Grant-store and audit persistence never recurse through themselves; unavailable required infrastructure denies effects                                     |
| Effect and terminal recording       | Crash after consumption remains unknown; a stored terminal outcome only reprojects; fencing a store never falsely proves an external effect stopped        |
| Budget accounting                   | Batch reservation is all-or-none; unknown started spend is retained; overruns and downward corrections remain truthful and idempotent                      |
| Compaction and assembly             | Cancellation preserves committed activation; rebuilt manifests use the returned cursor; generated prose cannot establish authoritative checkpoint facts    |
| Artifacts and reference commits     | Finalize/abort have one winner; lost reference acknowledgement cannot cause collection; a late commit cannot race a fenced deletion                        |
| Files and processes                 | Conditional target/binary identity holds against every writer in the declared isolation domain, or selection rejects the guarantee                         |
| Network and SDK adapters            | Reused connections honor the current peer grant; one-pass bodies stage before fingerprint-bound egress; no hidden retry or redirect bypasses authorization |
| Delegation and joins                | A waiting parent frees worker occupancy; one intent creates one child; recorded join winners replay identically                                            |
| Memory and deletion                 | Stale index hits and cached context cannot expose a tombstoned version; receipts distinguish logical deletion from physical purge                          |
| Hook timeout                        | An unquiesced invocation cannot mutate arguments observed by a later hook                                                                                  |

Tests cover both sides of each failure boundary, including lost acknowledgement
and successful commit followed by cancellation. Exact assertion targets are
public outcomes, durable receipts, content-free diagnostics, and
protected-effect counts, never private implementation calls. The architectural
API changes to run rejection/settlement, lane routing, and budget start/batch
states also require public API compatibility review before release.

## Agent evaluation

AgentKit.Evaluation is an optional package that runs datasets through the public
AgentEngine surface. It versions cases, fixtures, agent definitions, model
settings, evaluators, and expected criteria, then records results with the exact
provider, model, configuration, usage, latency, and trace identity.

Evaluators are independent capabilities. Deterministic evaluators check schemas,
tool effects, exact state, and safety properties. Model-based judges may assess
semantic quality but must declare their provider, model, prompt, rubric, repeat
count, and uncertainty. Evaluation storage and report exporters remain
replaceable leaf integrations when they need external systems.

The evaluation runner receives no privileged access to internal state. It uses
public results, event streams, session reads, manifests, and approved diagnostic
artifacts through the [artifact boundary](artifacts.md). This keeps the same
evaluation usable against first-party and custom implementations.

## Live verification

Live provider tests and evaluations are opt-in, credential-aware, time bounded,
and cost bounded. Missing credentials skip them clearly without weakening the
offline protocol suite. Test resources use isolated principals and are removed
according to the provider's retention contract.

The first-party live verification is `tests/AgentKit.Evaluation.Live.Tests`: a
`ModelJudgeEvaluator` over `ModelRequestJudgeClient` judging a real OpenAI
answer against a rubric. It runs only when `AGENTKIT_LIVE_TESTS=1` and
`OPENAI_API_KEY` are set (an optional `AGENTKIT_LIVE_OPENAI_MODEL` names the
model); otherwise xUnit reports it as skipped with that reason. It is bounded to
two minutes, a handful of requests, and the judge's own call and token budgets,
and uses only the in-memory result store.

## Contract and evaluation shape

Testing support consumes public contracts; it does not add test-only methods to
production services. Reusable suites in AgentKit.Conformance use a fixture that
creates the subject through its ordinary DI registration:

```csharp
namespace AgentKit.Conformance;

public interface IConformanceFixture<TContract> : IAsyncDisposable
    where TContract : class
{
    ConformanceCapabilities Capabilities { get; }

    ValueTask<TContract> CreateAsync(
        CancellationToken cancellationToken);
}

public sealed record ConformanceCapabilities(
    bool SupportsDurability = true,
    bool SupportsConcurrentCreators = true);
```

Capability flags permit a suite to omit only behavior the implementation
declared unsupported before use. `SupportsDurability` is false for subjects
whose acknowledged state does not survive disposal. `SupportsConcurrentCreators`
is false when overlapping `CreateAsync` calls are rejected. Both default to
true, so an omitted declaration still runs those optional cases. Flags cannot
skip required contract behavior or inspect private state.

Evaluation-specific provider-neutral contracts live in the optional
AgentKit.Evaluation package; core engine, result, event, session, identity, and
definition values remain in AgentKit.Abstractions:

```csharp
namespace AgentKit.Evaluation;

public readonly record struct EvaluationRunId(Guid Value);
public readonly record struct EvaluationPlanId(string Value);
public readonly record struct EvaluationCaseId(string Value);
public readonly record struct EvaluatorKey(string Value);
public readonly record struct EvaluationResultStoreKey(string Value);
public readonly record struct EvaluationReportExporterKey(string Value);

public sealed record EvaluationPlan(
    EvaluationPlanId Id,
    EvaluationPlanVersion Version,
    ImmutableArray<EvaluationCase> Cases,
    EvaluationExecutionPolicy Execution,
    EvaluationRecordingPolicy Recording);

public sealed record EvaluationCaseExecution(
    ExecutionIdentity Identity,
    SessionProfileKey SessionProfile);

public sealed record EvaluationCase(
    EvaluationCaseId Id,
    AgentId AgentId,
    EvaluationCaseExecution Execution,
    AgentInput Input,
    AgentRunOptions RunOptions,
    ImmutableArray<EvaluatorReference> Evaluators,
    EvaluationCriteria Criteria,
    EvaluationFixtureReference? Fixture);

public interface IEvaluationRunner
{
    Task<EvaluationReport> RunAsync(
        EvaluationPlan plan,
        CancellationToken cancellationToken);
}

public interface IEvaluator
{
    EvaluatorDescriptor Descriptor { get; }

    ValueTask<EvaluationOutcome> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken);
}

public interface IEvaluationResultStore
{
    ValueTask<EvaluationStoreResult> AppendAsync(
        EvaluationCaseResult result,
        CancellationToken cancellationToken);
}

public interface IEvaluationResultStoreSelector
{
    ValueTask<EvaluationResultStoreSelection> SelectAsync(
        EvaluationResultStoreKey key,
        CancellationToken cancellationToken);
}

public interface IEvaluationReportExporter
{
    ValueTask<EvaluationExportResult> ExportAsync(
        EvaluationReport report,
        CancellationToken cancellationToken);
}
```

`EvaluationRunId`, `EvaluationPlanId`, and `EvaluationCaseId` are validated
readonly identities. Each case names an `AgentId` from the one engine-wide
definition catalog and explicitly carries its already-authenticated execution
identity and expected session profile; it never mutates a singleton "current
agent" or substitutes a second catalog. The runner verifies that the resolved
definition selects that session profile before it creates the case session.
`EvaluationOutcome` discriminates passed, failed, inconclusive, skipped by
declared precondition, cancelled, unsupported, and evaluator failure. The report
preserves the underlying typed `RunId`, `SessionId`, trace correlation,
configuration/model manifest, usage, and safe diagnostics.

`EvaluationRunId` is generated through its closed identifier generator for each
execution. `EvaluationPlanId`, `EvaluationCaseId`, and evaluator/store/exporter
keys are stable, validated dataset/configuration keys and are never generated
run identities.

### Implemented value shapes (previously unspecified)

The contracts above name several values only in prose. Their implemented shapes
live in `AgentKit.Evaluation`; each is an immutable validated value, one type
per file, with value equality (hand-written where a member is an
`ImmutableArray`).

```csharp
namespace AgentKit.Evaluation;

// Plan side.
public readonly record struct EvaluationPlanVersion(long Value);          // positive
public readonly record struct EvaluatorVersion(long Value);               // positive
public readonly record struct EvaluationCriterionKey(string Value);
public abstract record EvaluationCriterion(EvaluationCriterionKey Key);   // sealed derived records
public sealed record EvaluationCriteria(ImmutableArray<EvaluationCriterion> Items); // one per key
public sealed record EvaluationFixtureReference(string Key, string Version);
public sealed record EvaluatorReference(EvaluatorKey Key, EvaluatorVersion? RequiredVersion);
public sealed record EvaluationExecutionPolicy(
    int MaximumConcurrentCases, int Repetitions, TimeSpan? CaseTimeout, TimeSpan? PlanDeadline,
    int? MaximumCaseRuns, bool StopOnEvaluatorFailure, bool PermitUnsupportedEvaluators);
public sealed record EvaluationRecordingPolicy(
    EvaluationResultStoreKey? ResultStore, ImmutableArray<EvaluationReportExporterKey> Exporters);

// Evaluator side.
public sealed record EvaluatorDescriptor(
    EvaluatorKey Key, EvaluatorVersion Version, string DisplayName,
    ImmutableArray<EvaluationCriterionKey> SupportedCriteria, bool RequiresFixture);
public sealed record EvaluationContext(
    EvaluationRunId RunId, EvaluationPlanId PlanId, EvaluationPlanVersion PlanVersion,
    EvaluationCase Case, int Repetition, AgentRunResult<ValidatedOutput> Result,
    EvaluationRunManifest Manifest, EvaluationUsageSummary Usage, TimeSpan Latency);
public sealed record EvaluationScore(double Value, int SampleCount, double StandardDeviation);

public abstract record EvaluationOutcome;   // Name, Summary, Score?, Evidence (safe named facts)
public sealed record EvaluationPassed(EvaluationScore? Score, string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);
public sealed record EvaluationFailed(EvaluationScore? Score, string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);
public sealed record EvaluationInconclusive(EvaluationScore? Score, string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);
public sealed record EvaluationSkipped(string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);   // declared precondition
public sealed record EvaluationCancelled(string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);
public sealed record EvaluationUnsupported(string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);
public sealed record EvaluatorFaulted(string ErrorType, string Summary, ImmutableArray<EvaluationEvidence> Evidence = default);

public sealed record EvaluatorResult(
    EvaluatorKey Key, EvaluatorVersion Version, EvaluationOutcome Outcome, TimeSpan Duration);

// Recorded evidence.
public sealed record EvaluationRunManifest(
    AgentId AgentId, AgentDefinitionRevision DefinitionRevision, AgentCatalogVersion CatalogVersion,
    SessionProfileKey SessionProfile, ImmutableArray<string> ModelCandidates,
    ImmutableArray<EvaluationModelUse> ModelsUsed);
public sealed record EvaluationUsageSummary(int ModelRequests, long? InputTokens, long? OutputTokens);
public sealed record EvaluationRunRecord(RunId RunId, SessionId SessionId, string Outcome, string Settlement);
public enum EvaluationCaseDisposition { Evaluated, RunRejected, Cancelled, TimedOut, Faulted }
public sealed record EvaluationCaseResult(
    EvaluationRunId EvaluationRunId, EvaluationPlanId PlanId, EvaluationPlanVersion PlanVersion,
    EvaluationCaseId CaseId, int CaseOrdinal, int Repetition, EvaluationCaseDisposition Disposition,
    DateTimeOffset StartedAt, TimeSpan Latency, string? TraceId, EvaluationRunRecord? Run,
    EvaluationRunManifest Manifest, EvaluationUsageSummary Usage, EvaluationFixtureReference? Fixture,
    ImmutableArray<EvaluatorResult> Evaluators, ImmutableArray<EvaluationDiagnostic> Diagnostics);
public enum EvaluationReportStatus { Completed, Cancelled, DeadlineExceeded, StoppedOnEvaluatorFailure }
public sealed record EvaluationReport(
    EvaluationRunId RunId, EvaluationPlanId PlanId, EvaluationPlanVersion PlanVersion,
    DateTimeOffset StartedAt, DateTimeOffset CompletedAt, EvaluationReportStatus Status,
    ImmutableArray<EvaluationCaseResult> Results, int NotStartedCaseRuns,
    ImmutableArray<EvaluationStoreAppendRecord> StoreResults,
    ImmutableArray<EvaluationExportRecord> ExportResults);

// Stores, selection, export.
public interface IEvaluationResultStore
{
    ValueTask<EvaluationStoreResult> AppendAsync(EvaluationCaseResult result, CancellationToken cancellationToken);
    ValueTask<EvaluationReadResult> ReadAsync(EvaluationResultQuery query, CancellationToken cancellationToken);
}
public abstract record EvaluationStoreResult;           // EvaluationStoreAppended(Receipt, Replayed) | EvaluationStoreRejected(Failure)
public abstract record EvaluationReadResult;            // EvaluationResultsRead(Results, Next) | EvaluationReadRejected(Failure)
public abstract record EvaluationResultStoreSelection;  // EvaluationResultStoreSelected(Key, Store) | EvaluationResultStoreUnavailable(Key, SafeMessage)
public abstract record EvaluationExportResult;          // EvaluationExported(Location?) | EvaluationExportRejected(Kind, SafeMessage)
public interface IEvaluatorCatalog { IEvaluator? Find(EvaluatorKey key); }
public interface IEvaluationReportExporterCatalog { IEvaluationReportExporter? Find(EvaluationReportExporterKey key); }
```

Resolved choices and deviations from the specification above, recorded so
reviewers do not rediscover them:

- **Runner dependency shape.** The first-party runner receives
  `IEvaluatorCatalog`, `IEvaluationResultStoreSelector`,
  `IEvaluationReportExporterCatalog`, the run-id generator, `TimeProvider`, the
  options snapshot, and an `ILogger`. Registration binds it to exactly one
  `AgentEngine` resolved from its own service provider; zero or several fail
  when the runner is first resolved, and a second `IEvaluationRunner`
  registration fails at registration. Options are validated and copied into the
  snapshot when the runner is first resolved (and at host start when a host
  validates options).
- **Plan validation fails before effects.** `RunAsync` throws
  `EvaluationPlanRejectedException`, carrying every typed
  `EvaluationPlanProblem`, before any session, run, store, or exporter effect
  when: the plan exceeds the concurrency, repetition, or case-run limits; a case
  names an agent the engine does not host; the resolved definition selects
  another session profile than the case declares; an evaluator is unregistered,
  pinned to another version, or cannot assess the case while the plan does not
  permit unsupported evaluators; or a named store or exporter is not registered.
  A token cancelled before validation throws `OperationCanceledException`
  instead.
- **Cancellation returns a partial report.** After effects begin, cancellation
  stops scheduling, flows to active runs and evaluators, and returns a report
  with `EvaluationReportStatus.Cancelled` (or `DeadlineExceeded` for the plan
  deadline, `StoppedOnEvaluatorFailure` when the plan asked to stop) holding the
  recorded repetitions; unscheduled repetitions are counted, never invented.
  Recording writes and report exports use their own bounded token (the default
  case timeout) independent of the caller, so computed evidence is never lost to
  a later cancellation; a cancelled run skips exporters and records them as
  cancelled.
- **Evaluator evidence travels with the outcome.** `IEvaluator` returns one
  `EvaluationOutcome`; its optional `Evidence` array is how a judge records its
  rubric and prompt fingerprint, so `EvaluatorResult` holds no separate
  evidence.
- **Built-in deterministic evaluators** use documented keys and criterion kinds:
  `schema` (`SchemaCriterion`, validated by the registered
  `IOutputSchemaEngine`), `exact-state` (`ExactStateCriterion`: outcome, text,
  JSON, message count), `tool-effect` (`ToolEffectCriterion`: required and
  forbidden calls over the run messages), and `safety` (`SafetyCriterion`:
  forbidden substrings and non-backtracking patterns under a one-second limit,
  required refusal markers). Results record which rule failed, never the matched
  or expected content.
- **Model judge.** `ModelJudgeEvaluator` (key `model-judge`, criterion
  `RubricCriterion`) requires an explicit judge model alias, takes `RepeatCount`
  sequential samples through the `IModelJudgeClient` seam, and reports the mean
  normalized score with its sample count and standard deviation; an incomplete,
  noisy, over-long, or unparseable judgement is inconclusive and a judge
  infrastructure failure is an evaluator failure. It records the configured and
  resolved model, provider, full rubric, scale, threshold, repeat count, SHA-256
  fingerprint of the exact prompt, and raw scores, never the candidate or the
  judge's reason. The first-party `ModelRequestJudgeClient` reaches the judge
  through the provider-neutral `IModelCatalog`, `IModelSelector`, and
  `ILlmModelResolver`, correlating the request to the judged run as an after-run
  operation. Deviations: the judge budget (`ModelJudgeBudget`: calls and
  reported tokens) is evaluator-local atomic accounting rather than a
  reservation through the hierarchical budget authority, because the evaluator
  holds no run scope; egress authority is that of the resolved model adapter
  rather than a separate `SecurityRequest`; and a single-candidate rubric has no
  candidate order, so blinded ordering does not apply.
- **Store shared source.** The three result-store leaves compile the linked
  source directories `AgentKit.Evaluation.Storage.Shared` (planner, in-memory
  state, observation, log events 36100-36102) and
  `AgentKit.Evaluation.Storage.Durable` (persisted documents and codec); they
  are not projects and are never selected.

- **Persisted results are a bounded projection.** A result store holds
  `EvaluationCaseResult`, not the run's messages. It preserves the typed `RunId`
  and `SessionId`, the trace identity of the case activity, the agent and model
  manifest, a usage summary, latency, fixture reference, evaluator results with
  evaluator version, and safe diagnostics. Prompts, model output, and tool data
  are never persisted, so a result store never becomes a second copy of session
  history. Evaluators see the full public `AgentRunResult<ValidatedOutput>`
  through `EvaluationContext`; only their typed outcome and safe evidence
  persist.
- **Usage stays unknown, not zero.** `EvaluationUsageSummary` token totals are
  present only when every model request in the run reported final usage for that
  dimension; a partial sum would present a wrong number as a measurement.
- **Store reads are part of the contract.** The specification shows only
  `AppendAsync`, but a conformance suite and any comparison must read results
  back through a public seam rather than private state. `ReadAsync` pages one
  run in deterministic case-ordinal then repetition order. Run discovery is
  carried by the returned `EvaluationReport` and exporters; the store does not
  list runs.
- **Result identity and idempotency.** A result is identified by evaluation run,
  case ordinal, and repetition. An identical repeat replays the original
  acknowledgement; a different result for that identity, a second plan identity
  or version for one run, or a second case at one ordinal is `IdentityConflict`.
- **Exporters and stores are resolved by key without a service locator in the
  runner.** The runner receives `IEvaluatorCatalog`,
  `IEvaluationResultStoreSelector`, and `IEvaluationReportExporterCatalog`;
  their first-party implementations resolve keyed registrations. This replaces
  the specification's `IEnumerable<IEvaluationReportExporter>`, which cannot be
  selected by key.
- **Fixtures are recorded evidence, not resolved resources.** The host composes
  the engine and fakes that realize a fixture. `EvaluationFixtureReference`
  travels with every result. There is no fixture resolver contract: no fixture
  implementation exists in the framework, an abstraction needs a demonstrated
  extension axis with at least two implementations, and the host that composes
  the engine already owns realizing and disposing its fixtures. The "fixture
  resolvers" item in the validation list is therefore not applicable; validation
  instead checks the part the runner does own, that an evaluator whose
  descriptor requires a fixture is never run on a case that declares none. A
  host that later needs resolution adds a contract in its owning package with
  its first two implementations.
- **Evaluators declare no artifact requirements.** An approved artifact arrives
  as a fixture or through the evaluator's own collaborators;
  `EvaluatorDescriptor` declares supported criteria and whether a fixture is
  required.

## First-party classes and service dependencies

| Package class                                                                          | Role and injected dependencies                                                                                                                                                                                                                               |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `EvaluationRunner` in AgentKit.Evaluation                                              | Uses one injected public multi-agent `AgentEngine`, `IIdentifierGenerator<EvaluationRunId>`, `TimeProvider`, configured concurrency/budget policy, result store selector, evaluators, and report exporters; it does not inject a separate definition catalog |
| `SchemaEvaluator`, `ExactStateEvaluator`, `ToolEffectEvaluator`, and `SafetyEvaluator` | Deterministic evaluators over public results, approved session reads, fixtures, and recorded effects                                                                                                                                                         |
| `ModelJudgeEvaluator`                                                                  | Optional evaluator using provider-neutral model catalog/request execution, an explicit judge model key, separate budget, security policy, and fully recorded rubric                                                                                          |
| External store/export packages                                                         | Implement `IEvaluationResultStore` or `IEvaluationReportExporter`; they use network/file/security contracts instead of direct host access                                                                                                                    |

The first-party runner's dependency shape is explicit; the implementation is
internal and direct implementations of `IEvaluationRunner` remain supported:

```csharp
namespace AgentKit.Evaluation;

internal sealed record EvaluationOptionsSnapshot(
    int MaximumConcurrentCases,
    int MaximumRepetitions,
    TimeSpan DefaultCaseTimeout);

internal sealed class EvaluationRunner(
    AgentEngine engine,
    IEvaluatorCatalog evaluators,
    IEvaluationResultStoreSelector stores,
    IEnumerable<IEvaluationReportExporter> exporters,
    IIdentifierGenerator<EvaluationRunId> evaluationRunIds,
    TimeProvider timeProvider,
    EvaluationOptionsSnapshot options) : IEvaluationRunner
{
    public Task<EvaluationReport> RunAsync(
        EvaluationPlan plan,
        CancellationToken cancellationToken) =>
        EvaluationExecution.RunAsync(
            engine,
            plan,
            evaluators,
            stores,
            exporters,
            evaluationRunIds,
            timeProvider,
            options,
            cancellationToken);
}
```

The runner receives no loop, mutable run context, provider SDK, raw service
provider, separate definition catalog, or privileged storage implementation. One
process-level engine can evaluate many agent definitions; each case resolves its
`AgentId`, checks the declared session profile, creates an ordinary session and
isolated run scope through that same engine, and observes only public results
and explicitly authorized artifacts.

## Lifetime, concurrency, and ownership

`EvaluationRunner` is normally a thread-safe singleton bound to the singleton
`AgentEngine` from the same composition. It creates bounded case work and relies
on that engine to create one scope per agent run. Stateless evaluators and
thread-safe stores/exporters may be singletons; an evaluator with mutable case
state is scoped or transient. No singleton captures a run scope.

The plan declares maximum concurrent cases, repetition, ordering, deadline,
provider/cost budget, and whether a failed evaluator stops later cases. Result
ordering is deterministic by plan/case/repetition identity even when execution
is parallel. Cancellation stops scheduling new cases, propagates to active agent
runs and evaluators, then records truthful cancellation/side-effect state. It
never deletes already persisted results. The DI owner disposes services;
fixtures own and dispose the provider or external resources they create.

## Dependency-injection registration

```csharp
namespace AgentKit.Evaluation;

public sealed class EvaluationOptions
{
    public int MaximumConcurrentCases { get; set; } = 4;
    public int MaximumRepetitions { get; set; } = 1;
    public TimeSpan DefaultCaseTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAgentEvaluation(
            Action<EvaluationOptions>? configure = null) =>
            EvaluationRegistration.AddDefault(services, configure);

        public IServiceCollection ReplaceEvaluationRunner<TRunner>()
            where TRunner : class, IEvaluationRunner =>
            EvaluationRegistration.ReplaceRunner<TRunner>(services);

        public IServiceCollection AddEvaluator<TEvaluator>(EvaluatorKey key)
            where TEvaluator : class, IEvaluator =>
            EvaluationRegistration.AddEvaluator<TEvaluator>(services, key);

        public IServiceCollection ReplaceEvaluator<TEvaluator>(EvaluatorKey key)
            where TEvaluator : class, IEvaluator =>
            EvaluationRegistration.ReplaceEvaluator<TEvaluator>(services, key);

        public IServiceCollection AddEvaluationResultStore<TStore>(
            EvaluationResultStoreKey key)
            where TStore : class, IEvaluationResultStore =>
            EvaluationRegistration.AddStore<TStore>(services, key);

        public IServiceCollection AddEvaluationReportExporter<TExporter>(
            EvaluationReportExporterKey key)
            where TExporter : class, IEvaluationReportExporter =>
            EvaluationRegistration.AddExporter<TExporter>(services, key);
    }
}
```

`AddAgentEvaluation` `TryAdd`s one singular, explicitly replaceable runner. It
validates the mutable binding options and captures one immutable
`EvaluationOptionsSnapshot` for the runner lifetime; the singleton never reads
an options monitor while an evaluation is active. The snapshot contains only
finite concurrency, repetition, and timeout mechanics; identity, session
profile, credentials, agent definition, and external destinations remain
explicit plan or keyed composition data. Registration requires exactly one
`AgentEngine` from the same service provider; a runner cannot accept an engine
argument later or pair that engine with another catalog. Evaluators are additive
and keyed by stable descriptor identity. Result stores are keyed and selected
explicitly by plan/options; there is no hidden durable store. Report exporters
are additive and keyed. Identical repeated registration is idempotent, while
conflicting key reuse fails startup rather than choosing the last registration.
Deterministic built-ins have documented keys and can be replaced deliberately
without replacing unrelated evaluators.

Evaluation result persistence uses explicit `AgentKit.Evaluation.InMemory`,
`AgentKit.Evaluation.Sqlite`, `AgentKit.Evaluation.Json`, or external store
leaves. All three first-party leaves run the same result-store conformance suite
(`EvaluationResultStoreConformanceTests`, supplied by an
`IEvaluationResultStoreConformanceFixture` that composes the store through its
public registration); SQLite and JSON claim durable local result retention only
after reopen tests pass, and JSON additionally proves torn-append recovery, root
identity and encoding-contract binding, and single-writer rejection. The
specification originally named only InMemory and SQLite; the repository-wide
rule that every persistent store family ships `.InMemory`, `.Sqlite`, and
`.Json` adapters under one suite settles that conflict in favor of all three.
Exporters remain separate effects and are never inferred to share a transaction
with the result store.

AgentKit.Conformance is a non-packable test library and has no production DI
registration. Each implementation test project supplies its fixture through the
same public `IServiceCollection` methods applications use.

## Build validation and unsupported behavior

Evaluation is optional and does not participate in normal engine validation
until registered. Its validation checks runner replacement ambiguity, case
concurrency and budgets, referenced evaluator/store/exporter keys, service
scopes, the single engine binding, agent/profile compatibility, trusted
evaluation identity, fixture requirements, and model-judge model capabilities.
An external store/exporter also activates validation for its
file/network/security/audit dependencies.

An evaluator declares supported criteria and required artifacts in its
descriptor. An incompatible plan fails before running cases, or yields a typed
unsupported/skipped outcome only where the plan explicitly permits it. Missing
live credentials cause a declared opt-in test skip; they do not make offline
conformance pass. Surprise `NotSupportedException`, access to mutable runtime
state, ambient clocks/IDs, public internet, real processes, and the developer's
workspace are all non-conforming defaults.

## Related specifications

- [Testing and evaluation](../concepts/testing-and-evaluation.md)
- [Project structure](project-structure.md)
- [Observability](observability.md)
