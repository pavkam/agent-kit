# WS1: Run envelope, admission, lanes, steering, attach and cancel

Goal: every run enters through durable admission, executes under the session
lane protocol, publishes a settlement-aware `AgentRunFinished<T>` envelope
through the documented `Agent.RunAsync<T>` / `StreamAsync<T>` surface, and can
receive steering and follow-up input while running.

Owning documents: [Agent runtime](../architecture/agent-runtime.md),
[Composition and configuration](../architecture/composition-and-configuration.md)
(process-level surface at lines 272–445),
[Input and output](../architecture/input-and-output.md),
[Loop state machine](../concepts/agent-loop-state-machine.md),
[Run lifecycle and settlement](../concepts/run-lifecycle-and-settlement.md),
[Session execution lanes](../concepts/session-execution-lanes.md).

## Progress

- [x] Prerequisite: `ISessionStore.LoadLaneStateAsync` (`fc540a98`)
- [x] Prerequisite: real `RunPolicyVersion` producer (`9c7ca08a`, `7e01b27e`)
- [x] Prerequisite: `DefaultAgentLoop` lane-aware, `LoopLaneAdmission`
      (`4288752b`)
- [x] Prerequisite: `AgentEngine.SendAgentAsync` drives
      provision/admit/accept/release (`3b209f27`)
- [x] Prerequisite: `ISessionStore.LoadPendingInputsAsync` and
      `PromoteInputAsync` across InMemory/Sqlite/Json with conformance
      (`d54a38c9`, `aa4e46a8`)
- [x] Prerequisite: `SessionBackedInputQueue` in `AgentKit.IO` (`05bdea28`)
- [ ] WS1-C1 lane identity and live revision in the loop
- [ ] WS1-C2 scoped `IInputCoordinator` and `SessionExecutionCapability`
- [ ] WS1-C3 loop promotes input at three boundaries
- [ ] WS1-C4 `IRunEventSink`, registration, backpressure contracts
- [ ] WS1-C5 `DefaultOutputPublisher` and `AddAgentIO`
- [ ] WS1-C6 loop publishes `RunEvent`s
- [ ] WS1-C7 unified outcome family
- [ ] WS1-C8 facade result types and `AgentRunOptions` reshape
- [ ] WS1-C9 `AgentEngineRuntime`, run plan, `RunAsync<T>`, delete
      `SessionLaneRegistry`
- [ ] WS1-C10 `SteerAsync` and `FollowUpAsync`
- [ ] WS1-C11 durable abort store primitive
- [ ] WS1-C12 `CancelAsync` and `AttachAsync` by `RunId`
- [ ] WS1-C13 Conversations over the engine, Simple sugar
- [ ] WS1-C14 input-queue conformance across three stores

## Verified current state

### Outcome families

| Type                                                                                                                                                                                                                                                                                              | State                      | Evidence                                                                                                                                                                                                                                  |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AgentRunOutcome` base                                                                                                                                                                                                                                                                            | EXISTS-AND-USED            | `src/AgentKit.Abstractions/Loop/AgentRunOutcome.cs:28`; both families derive from it                                                                                                                                                      |
| 13 `Loop/AgentRun*` terminals (`AgentRunCompleted`, `Cancelled`, `Failed`, `Idle`, `TurnLimitReached`, `BudgetExhausted`, `OutputRejected`, `OutputLengthLimitReached`, `AuthorizationUnavailable`, `ContextPreparationFailed`, `ModelSelectionFailed`, `SessionOperationFailed`, `InvalidState`) | EXISTS-AND-USED            | produced in `src/AgentKit.Loop/DefaultAgentLoop.cs`, `RunBudget.cs`, `DefaultRunContinuationPolicy.cs`; consumed by `src/AgentKit.Conversations/DefaultConversationSession.cs:622-687`, `src/AgentKit/EngineDelegationChannel.cs:120-122` |
| `Results/Run*` (`RunSucceeded`, `RunIdle`, `RunDeferred`, `RunCancelled`, `RunLimitReached`, `RunPolicyHalted`, `RunFailed` + causes)                                                                                                                                                             | EXISTS-UNWIRED             | `src/AgentKit.Abstractions/Results/*.cs`; zero production producers                                                                                                                                                                       |
| `RunSettlementOutcome`, `RunSettlementCompleted`, `RunSettlementRecoveryRequired`                                                                                                                                                                                                                 | EXISTS-UNWIRED             | `Results/RunSettlement*.cs`                                                                                                                                                                                                               |
| `AgentRunResult<T>`, `AgentRunFinished<T>`, `AgentRunRejected<T>`, `AgentRunStreamStartResult<T>`, `IAgentRunStream<T>`                                                                                                                                                                           | EXISTS-UNWIRED (facade)    | only `src/AgentKit.IO/AgentRunOutputPublisher.cs`, `RunEventStream.cs`, `RunEventHub.cs` use them                                                                                                                                         |
| `AgentLoopResult`                                                                                                                                                                                                                                                                                 | EXISTS-AS-REDUCED-STAND-IN | `Loop/AgentLoopResult.cs`; missing `ConversationId`, `Settlement`, `PreviousCursor`, `Output`, `Usage` vs `agent-runtime.md:242-252`; built only at `DefaultAgentLoop.cs:2723`                                                            |
| `IOutputPublisher`                                                                                                                                                                                                                                                                                | EXISTS-UNWIRED             | `Results/IOutputPublisher.cs`; `AgentRunOutputPublisher` is never registered in DI                                                                                                                                                        |
| `RunEvent`, `ContentDeltaEvent`, `MessageCommittedEvent`                                                                                                                                                                                                                                          | EXISTS-UNWIRED             | `Input/RunEvent.cs`; the loop emits the separate `AgentRunEvent` family via `IAgentRunObserver` (`DefaultAgentLoop.cs:836,1406,1877,1968`)                                                                                                |

### Invocation and services

| Type                                                         | State                      | Evidence                                                                                                                                                                                                                                                  |
| ------------------------------------------------------------ | -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IAgentLoop.RunAsync(AgentRunRequest, AgentRunServices, CT)` | EXISTS-AS-REDUCED-STAND-IN | `Loop/IAgentLoop.cs`; `agent-runtime.md:286-309` documents the reduction                                                                                                                                                                                  |
| `AgentRunInvocation`                                         | MISSING                    | spec `agent-runtime.md:171-185`; needs `HookDispatchContext` (WS2) and `RunPolicySnapshot` (no spec)                                                                                                                                                      |
| `AgentRunServices`                                           | EXISTS-AS-REDUCED-STAND-IN | `Loop/AgentRunServices.cs`: 8 required + 4 optional; spec `agent-runtime.md:187-199` adds `IInputCoordinator`, `SessionExecutionCapability`, `IModelRequestExecutor`, `IToolExecutor`, `IOutputPublisher`, `IHookDispatcher`, `BudgetExecutionCapability` |
| `AgentRunServicesFactory.Compile`                            | EXISTS-AND-USED            | `src/AgentKit/AgentRunServicesFactory.cs:46`; pure resolver, registers nothing into the scope, does not resolve `IInputCoordinator`                                                                                                                       |
| `LoopLaneAdmission`                                          | EXISTS-AND-USED            | produced `AgentEngine.cs:379`, consumed `DefaultAgentLoop.cs:256,377,685,700`; its `OperationStateRevision` is read once                                                                                                                                  |
| `IInputCoordinator` / `DefaultInputCoordinator`              | EXISTS-UNWIRED (loop)      | `src/AgentKit.IO/DefaultInputCoordinator.cs`, registered singleton at `IO/ServiceExtensions.cs:62`; no loop or engine caller                                                                                                                              |
| `SessionBackedInputQueue`                                    | EXISTS-UNWIRED             | `IO/SessionBackedInputQueue.cs:27`; scoped registration requires a scoped `SessionExecutionCapability` nothing registers                                                                                                                                  |
| `PromotionBoundary`                                          | EXISTS                     | `Input/PromotionBoundary.cs`                                                                                                                                                                                                                              |
| `PromotedInputContinuationCause`                             | EXISTS-UNWIRED             | never produced by `DefaultAgentLoop.ContinuationCauses` (`:1216`)                                                                                                                                                                                         |

Loop state relevant to promotion: `currentVersion` is threaded through
`RunCoreAsync` (`:394,423,508,527,547`); `history.SourceCursor` carries branch,
version, sequence; a live `OperationStateRevision` is **not** tracked.
`DecideContinuationAsync` fabricates `new OperationStateRevision(turn)`
(`:1645`) and `new ExecutionLaneId(request.BranchId.Value)` (`:1641`) while the
engine uses `new ExecutionLaneId(sessionId.Value)` (`AgentEngine.cs:288`); the
two disagree today.

### Facade

| Member                                                                                                                                                                                                   | State                      | Evidence                                                                                                  |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | --------------------------------------------------------------------------------------------------------- |
| `Agent.RunAsync(AgentRunOptions)`                                                                                                                                                                        | EXISTS, bypasses lanes     | `src/AgentKit/Agent.cs:103` → `AgentEngine.RunAgentAsync` (`:452-508`), no lane protocol; 19 tests use it |
| `Agent.SendAsync(AgentSendRequest)`                                                                                                                                                                      | EXISTS-AND-USED            | `Agent.cs:135` → `SendAgentAsync` (`:254-426`) with lane protocol plus `SessionLaneRegistry` in front     |
| `RunAsync<T>`, `StreamAsync<T>`, `CreateSessionAsync`, `AttachAsync`, `CancelAsync`, `SteerAsync`, `FollowUpAsync`                                                                                       | MISSING                    | –                                                                                                         |
| `AgentEngine.GetAgentAsync` → `ValueTask<Agent?>`; `GetAgentsAsync` → `ImmutableArray<AgentDefinition>`                                                                                                  | EXISTS-AS-REDUCED-STAND-IN | spec returns `AgentResolution` and `AgentCatalogSnapshot` (`composition-and-configuration.md:396-403`)    |
| `AgentEngineRuntime`, `AgentResolution` family, `AgentSessionCreateRequest`, `AgentSessionCreationResult` family, `AgentRunPlan`, `IAgentRunPlanCompiler`, `IAgentRunScopeFactory`, `AgentRunScopeLease` | MISSING                    | –                                                                                                         |
| `SessionLaneRegistry`                                                                                                                                                                                    | EXISTS-AND-USED            | `src/AgentKit/SessionLaneRegistry.cs`; only `AgentEngine.cs:43,292`; 6 tests                              |
| `IIdentifierGenerator<InputId>`                                                                                                                                                                          | MISSING                    | `AgentEngine.cs:346` news a GUID inline                                                                   |

### IO package

`AddAgentIO`, `DefaultOutputPublisher`, `IOutputBackpressurePolicy`,
`IRunEventSink`, `RunEventSinkRegistration`, `AddRunEventSink<T>` are all
MISSING. `AgentIOOptions` exists minimally (`IO/AgentIOOptions.cs`).
`RunEventHub` is internal with a per-run constructor (`IO/RunEventHub.cs:20`)
and is not DI-constructible.

### Conversations and Simple

`DefaultConversationSession` builds its own `AgentRunServices` (`:368`),
resolves the keyed loop itself (`:616`) and calls it directly (`:620`),
bypassing the engine. `AgentKit.Simple.AskAsync`/`SendAsync` delegate to it
(`src/AgentKit.Simple/AgentEngineExtensions.cs:40-126`).

### Test doubles that break on contract changes

- `IAgentLoop`: `tests/AgentKit.Conversations.Tests/FakeAgentLoop.cs`,
  `tests/AgentKit.Tests/GatedAgentLoop.cs`, `ScopedRecordingAgentLoop.cs`,
  `CompositionTestData.cs:176`, inline in
  `AgentKit.Loop.Tests/ServiceExtensionsTests.cs` and
  `AgentKit.Tests/AgentEngineBuilderTests.cs`.
- `new AgentLoopResult(`: `AgentLoopResultTests.cs`,
  `DefaultConversationSessionTests.cs`, `CompositionTestData.cs`,
  `GatedAgentLoop.cs`, `ScopedRecordingAgentLoop.cs`.
- `new AgentRunServices(`: `AgentRunServicesTests.cs` (10),
  `DefaultAgentLoopTests.cs:3678,3717`.
- `Loop/AgentRun*` consumers: `DefaultAgentLoopTests.cs` (155 references), 24
  files in `AgentKit.Abstractions.Tests`, `DefaultRunContinuationPolicyTests`
  (19), `RunContinuationPolicyConformanceTests` (3),
  `DefaultConversationSessionTests` (19), `AgentEngineBuilderExtensionsTests`
  (3), `AgentKit.Abstractions.verified.txt` (28).
- `ISessionCoordinator` fakes (12) and `ISessionStore` fakes (3) are affected
  only by WS1-C11.
- `IInputCoordinator` and `IOutputPublisher` have no test fakes yet.

## Hidden prerequisites

1. Name collision: facade `AgentRunRequest` in the spec vs loop-level
   `AgentKit.AgentRunRequest` (`Abstractions/Loop/AgentRunRequest.cs:28`).
   Decide the rename before WS1-C8 (recommended: rename the loop type to
   `AgentLoopRunRequest`).
2. `AgentRunInvocation` cannot be built to spec until `HookDispatchContext`
   (WS2) exists; `RunPolicySnapshot` has no spec anywhere. Ship a reduced
   invocation or sequence after WS2.
3. `IModelRequestExecutor` (WS7) and `IToolExecutor` (WS4) are MISSING. Do not
   add throwaway adapters; keep `ILlmModelResolver` and `IToolInvoker` on
   `AgentRunServices` until those workstreams replace them.
4. Captive dependency: `DefaultInputCoordinator` is singleton but
   `SessionBackedInputQueue` is scoped; both must be scoped
   (`input-and-output.md:523`).
5. The engine builds `SessionExecutionCapability` after creating the scope
   (`AgentEngine.cs:274,289`); a scoped holder type is required to place it in
   the scope's provider.
6. `ExecutionLaneId` identity mismatch between engine and loop (see above).
7. `PromoteInputAsync` advances `OperationStateRevision`; the loop and the
   engine release path (`AgentEngine.cs:633` hard-codes revision 1) must carry
   the live value or release is rejected as stale.
8. The loop never emits `RunEvent`; `RunEvent` constructors reject sequence
   `< 1`, so the publisher must allocate sequences.
9. `RunUsage` is not accumulated; `new RunUsage(runId, [])` is valid interim.
10. `AgentRunFinished<T>` invariants over `NewMessages` (conversation, branch,
    run, state) must be verified against compaction-projected messages before
    the facade produces it.
11. `IRunEventSink` must live in `AgentKit.Abstractions`
    (`ProjectReferenceGraph.cs:158`; `observability.md:92`).
12. `AgentKit.Conversations` is a behavioral runtime and may not reference
    `AgentKit`; a per-session handle over the engine needs a reclassification
    decision (architecture test, `AGENTS.md` map, `project-structure.md`).
13. Cancel by `RunId` needs a durable abort store primitive; none exists in
    `ISessionStore` (`Sessions/ISessionStore.cs`).
14. Attach by `RunId` needs a process-local registry of active run publishers;
    none exists. Cross-process attach is out of scope.
15. `SessionRunCoordinatorConformanceTests` runs only for InMemory
    (`tests/AgentKit.Session.InMemory.Tests/InMemorySessionRunCoordinatorConformanceFixture.cs`).

## Spec coverage

| Contract                                                                                         | Spec                                         | Status                                             |
| ------------------------------------------------------------------------------------------------ | -------------------------------------------- | -------------------------------------------------- |
| unified `AgentLoopResult`                                                                        | `agent-runtime.md:242-252`                   | SPEC (has `ConversationId?`, not `BranchId`)       |
| `AgentRunInvocation`, full `AgentRunServices`, `IAgentLoop`                                      | `agent-runtime.md:171-199,254-259`           | SPEC; `RunPolicySnapshot` NO-SPEC                  |
| `Results/*`, `AgentRunFinished<T>`, `IOutputPublisher`, `IAgentRunStream<T>`, `RunEvent`         | `input-and-output.md:259-374`                | SPEC, all exist                                    |
| facade `AgentRunRequest`, `AgentSessionCreateRequest`, `AgentResolution`, `Agent`, `AgentEngine` | `composition-and-configuration.md:274-429`   | SPEC; `SessionCreationFailure` NO-SPEC             |
| `AgentRunPlan`, `IAgentRunPlanCompiler`, `IAgentRunScopeFactory`, `AgentRunScopeLease`           | `composition-and-configuration.md:482-536`   | SPEC; `AgentOptionalCapabilitySelection` from WS18 |
| `IAgentRunScopeValidator`                                                                        | –                                            | NO-SPEC                                            |
| `AgentRunOptions` as narrowing overrides                                                         | prose `composition-and-configuration.md:987` | NO-SPEC                                            |
| `DefaultOutputPublisher` ctor                                                                    | `input-and-output.md:494-501`                | SPEC; `IOutputBackpressurePolicy` members NO-SPEC  |
| `AddAgentIO` and related registrations                                                           | `input-and-output.md:531-570`                | SPEC; `RunEventSinkRegistration` members NO-SPEC   |
| `IRunEventSink`                                                                                  | `observability.md:104-109`                   | SPEC                                               |
| `SteerAsync`, `FollowUpAsync`, `AttachAsync`, `CancelAsync`, `OpenSessionAsync`, abort primitive | prose only                                   | NO-SPEC; design sections required first            |
| promotion at steps 2/11/12                                                                       | `agent-loop-state-machine.md:56-85`          | SPEC-prose; `InputPromotionRequest` exists         |

## Chunks

### WS1-C1: Fix lane identity and thread live revision in the loop

- Depends on: –. Risk: DENSE-MODIFY `DefaultAgentLoop.cs`
  `DecideContinuationAsync` (`:1641,1645`), `ReleaseLaneAsync` (`:330`),
  `RunCoreAsync` (~40 lines); `AgentEngine.ReleaseLaneAsync` (`:633`). Size: M.
- Deliverables: new internal `src/AgentKit.Loop/LoopLaneState.cs`
  (`ExecutionLaneId`, current `OperationStateRevision`); loop uses
  `request.LaneAdmission?.ExecutionLaneId ?? new ExecutionLaneId(request.SessionId.Value)`
  and the tracked revision everywhere; test
  `RunAsync_WhenLaneAdmitted_ContinuationContextUsesAdmittedLaneAndRevision`.
- Done when: `DefaultAgentLoopTests` and `AgentKit.Tests` pass; continuation
  context lane equals admission lane.

### WS1-C2: Scoped `IInputCoordinator` and `SessionExecutionCapability` holder

- Depends on: –. Risk: CONTRACT-BREAK (lifetime only; two lifetime assertions in
  `tests/AgentKit.IO.Tests/ServiceExtensionsTests.cs`). Size: M.
- Deliverables: internal scoped `src/AgentKit/RunScopeState.cs` holding
  `SessionExecutionCapability?` (and run identity for C5); `AddAgentKit()`
  registers it plus a scoped `SessionExecutionCapability` resolver and
  `IIdentifierGenerator<InputId>`; `AgentEngine.cs:346` uses the generator;
  `IO/ServiceExtensions.cs:62` becomes `TryAddScoped`; `SendAgentAsync` sets the
  holder after line 289; `AgentRunServices` gains optional
  `IInputCoordinator? Input`; `AgentRunServicesFactory.Compile` resolves it.
  Tests: `AgentRunServicesTests` +2, `AgentKit.Tests` +1, IO +2 modified.
  Snapshots: Abstractions, AgentKit, IO.
- Done when: an engine composed with `AddSessionBackedInputQueue` and
  `AddInputCoordinator` resolves `IInputCoordinator` inside the run scope.

### WS1-C3: `DefaultAgentLoop` calls `PromoteAsync` at three boundaries

- Depends on: C1, C2. Risk: DENSE-MODIFY `RunCoreAsync` (insert between `:423`
  and `:425`), `DecideContinuationAsync` (`:1614-1690`, ~60 lines), history
  refresh after promotion. Size: L.
- Deliverables: `BeforeFirstModelRequest` before model resolution;
  `AfterTurnCommitted` before the continuation policy call; `OtherwiseIdle` in
  the `CompleteRun` arm with at most
  `AgentLoopOptions.MaximumPromotionsPerBoundary`;
  `PromotedInputContinuationCause` produced; `LoopLog` events 1107–1112; new
  `tests/AgentKit.Loop.Tests/ScriptedInputCoordinator.cs`; five loop tests.
  Snapshot: Loop. Docs: `src/AgentKit.Loop/README.md`, agent-loop skill.
- Done when: with `services.Input == null` behavior is unchanged (existing test
  file untouched); the new tests pass.
- Open: confirm the loop must not re-append promoted messages (the store
  materializes them).

### WS1-C4: `IRunEventSink`, `RunEventSinkRegistration`, `IOutputBackpressurePolicy`

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `src/AgentKit.Abstractions/Results/IRunEventSink.cs`,
  `RunEventSinkRegistration.cs`, `RunEventDelivery.cs`,
  `IOutputBackpressurePolicy.cs`, `BackpressureDecision.cs`; write the NO-SPEC
  C# blocks into `input-and-output.md` first; tests in
  `AgentKit.Abstractions.Tests/Results/`. Snapshot: Abstractions.
- Open: exact `IOutputBackpressurePolicy` signature.

### WS1-C5: `DefaultOutputPublisher`, `AddAgentIO`, `AddRunEventSink<T>`

- Depends on: C4. Risk: ADDITIVE (`AgentRunOutputPublisher` kept until C10).
  Size: L.
- Deliverables: internal `src/AgentKit.IO/DefaultOutputPublisher.cs` per spec
  ctor, scoped `RunEventHub` factory from `RunScopeState`,
  `DefaultOutputBackpressurePolicy.cs`, `RunEventSinkBinding.cs`,
  `AgentIORegistration.cs`, `AddAgentIO` / `AddInputCoordinator<T>` /
  `ReplaceInputCoordinator<T>` / `AddOutputPublisher<T>` /
  `ReplaceOutputPublisher<T>` / `AddRunEventSink<T>`; optional
  `ComponentKey<IOutputPublisher>?` on `AgentDefinition` with validator check.
  Tests: required-sink failure, best-effort isolation, idempotent registration,
  duplicate sink name. Snapshots: IO, Abstractions.
- Done when: `AddAgentIO` twice is idempotent; duplicate sink fails build.

### WS1-C6: Loop publishes `RunEvent`s through the publisher

- Depends on: C5. Risk: DENSE-MODIFY `DefaultAgentLoop.ObserveAsync`
  (`:1877-1910`) and commit sites (`:1994`, `:1289`, ~50 lines); rename
  `AgentRunServices.Output` (`IOutputProcessor?`) to `OutputProcessor` and add
  `IOutputPublisher? Publisher` (breaks `AgentRunServicesTests`,
  `DefaultConversationSession.cs:655-659`). Size: M.
- Deliverables: publisher-side sequence allocation; three loop tests with a
  recording publisher. Snapshots: Abstractions, Loop.
- Open: who stamps `Sequence` on immutable `RunEvent` records.

### WS1-C7: Unified outcome family (the documented break)

- Depends on: C1. Risk: CONTRACT-BREAK across every file in the test-doubles
  list above plus `DefaultConversationSession.cs`, `EngineDelegationChannel.cs`,
  `RunBudget.cs`, `DefaultRunContinuationPolicy.cs`,
  `ArgumentExceptionExtensions.cs`. Size: L. Never combine with another chunk.
- Deliverables: `AgentLoopResult` to `agent-runtime.md:242-252`; delete the 13
  `Loop/AgentRun*` records; mapping: `Completed→RunSucceeded`, `Idle→RunIdle`,
  `Cancelled→RunCancelled`, limits→`RunLimitReached`,
  failures→`RunFailed(RunFailure)` with typed cause preserved,
  `OutputRejected→RunPolicyHalted`; settlement `RunSettlementCompleted` when a
  final version exists else `RunSettlementRecoveryRequired`; usage
  `new RunUsage(runId, [])`. Snapshots: Abstractions, Loop, Conversations,
  AgentKit. Docs: `agent-runtime.md:286-309`, `guides/composition.md`,
  `use-cases/web-support-assistant.md`, agent-loop skill.
- Done when: no `AgentRun(Completed|Cancelled|…)` identifier remains in `src/`;
  full solution green.
- Open: `RunLimitFailure` for non-budget limits; where
  `ContextPreparationFailure`, `ProviderFailure`, model-selection diagnostics
  live inside `RunFailure`.

### WS1-C8: Facade result types and `AgentRunOptions` reshape

- Depends on: C7. Risk: CONTRACT-BREAK (`GetAgentAsync`, `GetAgentsAsync` return
  types, `AgentRunOptions` ctor; `AgentTests` 19 `RunAsync_*`,
  `AgentEngineTests`, `AgentEngineBuilderTests`, `AgentRunOptionsTests`,
  `CompositionTestData.RunOptions`, `EngineDelegationChannelTests`,
  `QuickStart.Tests`, `CodingAgent.Tests`). Size: L.
- Deliverables: rename loop `AgentRunRequest` first; new facade
  `AgentResolution`, `ResolvedAgent`, `AgentNotFound`, `InvalidAgent`,
  `AgentSessionCreateRequest`, `AgentSessionCreationResult`,
  `AgentSessionCreated`, `AgentSessionCreationFailed`,
  `Abstractions/Sessions/SessionCreationFailure.cs` (write spec block), facade
  `AgentRunRequest`; `AgentRunOptions` becomes
  `(int? MaxTurns, TimeSpan? AttemptTimeout)`; public
  `AgentEngine.CreateSessionAsync`. Snapshots: AgentKit, Abstractions. Docs:
  rewrite `composition-and-configuration.md:445-473`.
- Done when: the records at `composition-and-configuration.md:274-313` compile
  verbatim.

### WS1-C9: `AgentEngineRuntime`, run plan, `RunAsync<T>`, `StreamAsync<T>`

- Depends on: C2, C3, C5, C6, C8. Risk: DENSE-MODIFY `AgentEngine.cs:254-508`
  (~250 lines move into the runtime); CONTRACT-BREAK on
  `AgentSessionBusyException` (becomes `AgentRunRejected<T>`; `AgentTests` 4,
  `AgentSessionBusyExceptionTests` 6, `SessionLaneRegistryTests` deleted). Size:
  L.
- Deliverables: `src/AgentKit/AgentEngineRuntime.cs`,
  `Internal/AgentRunPlan.cs`, `AgentRunPlanCompilationResult.cs`,
  `CompiledAgentRunPlan.cs`, `InvalidAgentRunPlan.cs`,
  `IAgentRunPlanCompiler.cs`, `DefaultAgentRunPlanCompiler.cs`,
  `IAgentRunScopeFactory.cs`, `AgentRunScopeFactory.cs`,
  `AgentRunScopeLease.cs`; `AgentRunServicesFactory` folds into the compiler;
  `Agent.RunAsync<T>`, `StreamAsync<T>`, `CreateSessionAsync`; `SendAsync`
  becomes a wrapper; lane-bypassing `RunAgentAsync` removed;
  `SessionLaneRegistry` deleted. Snapshot: AgentKit. Docs:
  `composition-and-configuration.md:440-443`, `AGENTS.md`,
  `src/AgentKit/README.md`.
- Done when: `rg SessionLaneRegistry src` is empty; lane contention returns
  `AgentRunRejected<T>` with zero store appends.
- Open: whether `Wait` busy behavior survives as an in-process gate inside the
  runtime.

### WS1-C10: `Agent.SteerAsync` and `Agent.FollowUpAsync`

- Depends on: C9. Risk: ADDITIVE, NO-SPEC (write the signatures into
  `composition-and-configuration.md` first). Size: M.
- Deliverables: `AgentEngineRuntime.AdmitInputAsync` (fresh
  `BeforeRunOperationCorrelation`, fresh authorization capture, new scope with
  `RunScopeState.Session` set, `IInputCoordinator.AdmitAsync`); two `Agent`
  methods; end-to-end tests with `DefaultAgentLoop` and the InMemory store:
  steering visible on the next turn, follow-up admitted after settlement.
  Snapshot: AgentKit. Docs: input-output skill.

### WS1-C11: Durable abort store primitive

- Depends on: – (parallel with C1–C6); NO-SPEC (write `SessionRunAbort*` C#
  block in `architecture/sessions.md` and the lanes concept first). Risk:
  CONTRACT-BREAK on `ISessionStore` and `ISessionCoordinator` (3 stores,
  `DefaultSessionCoordinator`, 3 fake stores, 12 coordinator fakes,
  conformance). Size: L.
- Deliverables: `Sessions/SessionRunAbortRequest.cs`,
  `SessionRunAbortResult.cs`, `SessionRunAbortRecorded.cs`,
  `SessionRunAbortRejected.cs`, `SessionRunAbortRejectionKind.cs`; each store
  commits a cancel marker, prunes pending admissions atomically, advances
  revision; `LoadRunStateAsync` exposes `AbortRequested`; four conformance
  cases. Snapshots: Abstractions, Session.\*.

### WS1-C12: `CancelAsync(RunId)` and `AttachAsync(RunId)`

- Depends on: C6, C9, C11. Risk: DENSE-MODIFY loop polls `LoadRunStateAsync` at
  each C3 boundary and settles `RunCancelled` (~40 lines). Size: L.
- Deliverables: internal `src/AgentKit/ActiveRunRegistry.cs`;
  `Agent.CancelAsync`; `Agent.AttachAsync<T>` returning
  `AgentRunStreamStartResult<T>` with replay synthesized from
  `ISessionStore.ReadAsync` plus live tail; tests: cancel settles and releases
  the lane; attach after settlement is rejected; attach mid-run receives replay
  and tail. Snapshot: AgentKit. Docs: new section in
  `composition-and-configuration.md`.

### WS1-C13: Conversations over the engine, Simple sugar

- Depends on: C9, C10, C12, and the dependency-direction decision (prerequisite
  12). Risk: CONTRACT-BREAK (`DefaultConversationSession` rewrite,
  `AgentKit.Simple.Tests`, architecture test classification). Size: L.
- Deliverables: `AgentEngine.OpenSessionAsync` with spec block;
  `DefaultConversationSession` delegates to `Agent.RunAsync<T>`; delete its
  private service bundle; Simple `AttachAsync`, `CancelAsync`, `StreamAsync`.
  Snapshots: Conversations, Simple, AgentKit. Docs: `getting-started.md`, use
  cases, READMEs.

### WS1-C14: Input-queue conformance across InMemory, Sqlite, Json

- Depends on: C2. Risk: ADDITIVE. Size: M.
- Deliverables: `tests/AgentKit.Conformance/IInputQueueConformanceFixture.cs`,
  `InputQueueConformanceTests.cs`; fixtures in the three `Session.*.Tests`
  projects; `SessionRunCoordinatorConformanceTests` fixtures for Sqlite and
  Json.

## Totals

S 1, M 5, L 8. Confidence medium-low on hidden scope: three NO-SPEC surfaces
(`AgentRunOptions` shape, steer/follow-up/attach/cancel, abort primitive), one
architecture decision (Conversations dependency direction), one cross-workstream
dependency (WS2 `HookDispatchContext`), and the `RunLimitFailure` mismatch.
