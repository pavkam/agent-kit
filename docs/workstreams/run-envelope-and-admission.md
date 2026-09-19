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
- [x] WS1-C1 lane identity and live revision in the loop
- [x] WS1-C2 scoped `IInputCoordinator` and `SessionExecutionCapability`
- [x] WS1-C3 loop promotes input at three boundaries
- [x] WS1-C4 `IRunEventSink`, registration, backpressure contracts
- [x] WS1-C5 `DefaultOutputPublisher` and `AddAgentIO`
- [x] WS1-C6 loop publishes `RunEvent`s
- [x] WS1-C7 unified outcome family
- [x] WS1-C8 facade result types and `AgentRunOptions` reshape
- [ ] WS1-C9 `AgentEngineRuntime`, run plan, `RunAsync<T>`, delete
      `SessionLaneRegistry`
- [ ] WS1-C10 `SteerAsync` and `FollowUpAsync`
- [ ] WS1-C11 durable abort store primitive
- [ ] WS1-C12 `CancelAsync` and `AttachAsync` by `RunId`
- [ ] WS1-C13 Conversations over the engine, Simple sugar
- [ ] WS1-C14 input-queue conformance across three stores

## Verified current state

### Outcome families

| Type                                                                                                                                  | State                      | Evidence                                                                                                                                                                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------------------------------------- | -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `AgentRunOutcome` base                                                                                                                | EXISTS-AND-USED            | `src/AgentKit.Abstractions/Loop/AgentRunOutcome.cs`; closed to the 7 canonical cases (WS1-C7)                                                                                                                                                                                              |
| ~~13 `Loop/AgentRun*` terminals~~                                                                                                     | DELETED                    | removed in WS1-C7; `rg` for `AgentRun(Completed\|Cancelled\|Failed\|Idle\|TurnLimitReached\|BudgetExhausted\|OutputRejected\|OutputLengthLimitReached\|AuthorizationUnavailable\|ContextPreparationFailed\|ModelSelectionFailed\|SessionOperationFailed\|InvalidState)` in `src/` is empty |
| `Results/Run*` (`RunSucceeded`, `RunIdle`, `RunDeferred`, `RunCancelled`, `RunLimitReached`, `RunPolicyHalted`, `RunFailed` + causes) | EXISTS-AND-USED            | `src/AgentKit.Abstractions/Results/*.cs`; produced by `src/AgentKit.Loop/DefaultAgentLoop.cs` (via `RunOutcomes.cs`), `RunBudget.cs`, `DefaultRunContinuationPolicy.cs`; consumed by `DefaultConversationSession.cs`, `EngineDelegationChannel.cs` (WS1-C7)                                |
| `RunSettlementOutcome`, `RunSettlementCompleted`, `RunSettlementRecoveryRequired`                                                     | EXISTS-AND-USED (WS1-C9)   | `Results/RunSettlement*.cs`; `DefaultAgentLoop.BuildResult` always produces `RunSettlementCompleted`; `RunSettlementRecoveryRequired` still has no producer, deferred to a future durable-recovery chunk                                                                                   |
| `AgentRunResult<T>`, `AgentRunFinished<T>`, `AgentRunRejected<T>`, `AgentRunStreamStartResult<T>`, `IAgentRunStream<T>`               | EXISTS-UNWIRED (facade)    | only `src/AgentKit.IO/AgentRunOutputPublisher.cs`, `RunEventStream.cs`, `RunEventHub.cs` use them                                                                                                                                                                                          |
| `AgentLoopResult`                                                                                                                     | EXISTS-AS-REDUCED-STAND-IN | `Loop/AgentLoopResult.cs`; gained `Output` (WS1-C7), `Usage`/`Settlement` (WS1-C9); `ConversationId`/`PreviousCursor` deliberately NOT added here — WS1-C9's Landed note explains why those belong at the facade instead; built only at `DefaultAgentLoop.BuildResult`                     |
| `IOutputPublisher`                                                                                                                    | EXISTS-UNWIRED             | `Results/IOutputPublisher.cs`; `AgentRunOutputPublisher` is never registered in DI                                                                                                                                                                                                         |
| `RunEvent`, `ContentDeltaEvent`, `MessageCommittedEvent`                                                                              | EXISTS-UNWIRED             | `Input/RunEvent.cs`; the loop emits the separate `AgentRunEvent` family via `IAgentRunObserver` (`DefaultAgentLoop.cs:836,1406,1877,1968`)                                                                                                                                                 |

### Invocation and services

| Type                                                         | State                      | Evidence                                                                                                                                                                                                                                                          |
| ------------------------------------------------------------ | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `IAgentLoop.RunAsync(AgentRunRequest, AgentRunServices, CT)` | EXISTS-AS-REDUCED-STAND-IN | `Loop/IAgentLoop.cs`; `agent-runtime.md:286-309` documents the reduction                                                                                                                                                                                          |
| `AgentRunInvocation`                                         | MISSING                    | spec `agent-runtime.md:171-185`; needs `HookDispatchContext` (WS2) and `RunPolicySnapshot` (no spec)                                                                                                                                                              |
| `AgentRunServices`                                           | EXISTS-AS-REDUCED-STAND-IN | `Loop/AgentRunServices.cs`: 9 required/optional-with-default + 3 optional; spec `agent-runtime.md:187-199` still needs `SessionExecutionCapability`, `IModelRequestExecutor`, `IToolExecutor`, `IOutputPublisher`, `IHookDispatcher`, `BudgetExecutionCapability` |
| `AgentRunServicesFactory.Compile`                            | EXISTS-AND-USED            | `src/AgentKit/AgentRunServicesFactory.cs:46`; resolves `IInputCoordinator` (WS1-C2); registers nothing into the scope itself — `RunScopeState` is populated by its caller first                                                                                   |
| `LoopLaneAdmission`                                          | EXISTS-AND-USED            | produced `AgentEngine.cs:379`, consumed `DefaultAgentLoop.cs` to seed `LoopLaneState` (WS1-C1)                                                                                                                                                                    |
| `LoopLaneState`                                              | EXISTS-AND-USED            | `Loop/LoopLaneState.cs` (WS1-C1); one instance per run threaded through `RunCoreAsync`→`RunTurnAsync`→`SettleCompletedAsync`/`ValidateOutputAsync`/`InvokeToolsAsync`→`DecideContinuationAsync`, and into `ReleaseLaneAsync`                                      |
| `RunScopeState`                                              | EXISTS-AND-USED            | `src/AgentKit/RunScopeState.cs` (WS1-C2); scoped holder, `Session` set by `AgentEngine.SendAgentAsync` before `AgentRunServicesFactory.Compile` runs                                                                                                              |
| `IInputCoordinator` / `DefaultInputCoordinator`              | EXISTS-AND-USED            | `src/AgentKit.IO/DefaultInputCoordinator.cs`, scoped at `IO/ServiceExtensions.cs` (WS1-C2); resolved by `AgentRunServicesFactory.Compile` into `AgentRunServices.Input`; called by `DefaultAgentLoop.TryPromoteInputAsync` at all three boundaries (WS1-C3)       |
| `SessionBackedInputQueue`                                    | EXISTS-AND-USED            | `IO/SessionBackedInputQueue.cs:27`; scoped `SessionExecutionCapability` now resolves through `RunScopeState` (WS1-C2)                                                                                                                                             |
| `PromotionBoundary`                                          | EXISTS                     | `Input/PromotionBoundary.cs`                                                                                                                                                                                                                                      |
| `PromotedInputContinuationCause`                             | EXISTS-AND-USED            | produced by `DefaultAgentLoop.ContinuationCauses` when `TryPromoteInputAsync` commits a promotion at `AfterTurnCommitted` (WS1-C3)                                                                                                                                |

Loop state relevant to promotion: `currentVersion` is threaded through
`RunCoreAsync`; `history.SourceCursor` carries branch, version, sequence.
`LoopLaneState` (WS1-C1) tracks the run's one live `ExecutionLaneId` and
`OperationStateRevision`: seeded from `request.LaneAdmission` when present, or
`new ExecutionLaneId(request.SessionId.Value)` / `new OperationStateRevision(1)`
otherwise, matching the engine's own derivation (`AgentEngine.cs:288`).
`DecideContinuationAsync` reads both from `laneState` instead of fabricating
`new OperationStateRevision(turn)` and
`new ExecutionLaneId(request.BranchId.Value)`. WS1-C3 wires the mid-run advance:
`DefaultAgentLoop.TryPromoteInputAsync` calls `services.Input.PromoteAsync` at
`BeforeFirstModelRequest` (once, in `RunCoreAsync` before model resolution),
`AfterTurnCommitted` (in `DecideContinuationAsync`, before the continuation
policy call), and `OtherwiseIdle` (only when the policy would otherwise
`CompleteRun` and a further turn is possible). A committed promotion's messages
are never re-appended by the loop — the session store alone materializes them —
so `TryPromoteInputAsync` reloads the newly visible entries by paging
`services.Session.ReadAsync` forward from the pre-promotion cursor.
`ReleaseLaneAsync` releases using `laneState`'s current revision, which
`DecideContinuationAsync`/`RunCoreAsync` now update in place once a promotion
commits, so a mid-run advance releases correctly without a further change to the
release path.

Two correctness fixes were required to land this without violating
`ArgumentExceptionExtensions.ThrowIfInconsistentContinuationEvidence`:

1. `DefaultAgentLoop.RunAsync` now reuses
   `request.LaneAdmission?.AcceptedCorrelation.OperationId` as the run's own
   driving `OperationId` instead of always minting an unrelated one via
   `_operationIds.Create()`. The store validates every promotion and release
   against the exact operation admission installed
   (`SessionAcceptedRunState.Correlation.OperationId`); the loop's own
   `RunContinuationContext.OperationId` must therefore be that same identity
   whenever the run is durably admitted, not a second, disconnected value that
   happened never to be cross-checked before `PromotedInputContinuationCause`
   existed.
2. `RunContinuationContext`'s `operationStateRevision`, `branchCursor`, and
   `InputPromotionCutoff` describe the evidence **as observed before** the
   `AfterTurnCommitted` promotion attempt — matching what
   `PromotedInputContinuationCause.Snapshot` itself carries — not the store's
   new state after the commit. `DecideContinuationAsync` therefore builds the
   context from the pre-promotion `nextCursor`/`lastEntryId`/
   `laneState.OperationStateRevision`, and only afterward applies the commit's
   advanced revision, merged messages, and merged cursor to what a `ContinueRun`
   decision actually resumes from and what the run's settlement version reports.

### Facade

| Member                                                                                                           | State                    | Evidence                                                                                                                           |
| ---------------------------------------------------------------------------------------------------------------- | ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `Agent.RunAsync(SessionId, BranchId, ExecutionIdentity, AgentRunOptions?)`                                       | EXISTS, bypasses lanes   | `src/AgentKit/Agent.cs` → `AgentEngine.RunAgentAsync`, no lane protocol; reshaped in WS1-C8                                        |
| `Agent.SendAsync(AgentSendRequest)`                                                                              | EXISTS-AND-USED          | `Agent.cs` → `SendAgentAsync` with lane protocol plus `SessionLaneRegistry` in front                                               |
| `Agent.CreateSessionAsync`, `AgentEngine.CreateSessionAsync`                                                     | EXISTS-AND-USED (WS1-C8) | independent admission path, not layered on `SendAgentAsync`; typed `AgentSessionCreationResult`                                    |
| `RunAsync<T>`, `StreamAsync<T>`, `AttachAsync`, `CancelAsync`, `SteerAsync`, `FollowUpAsync`                     | MISSING                  | –                                                                                                                                  |
| `AgentResolution` family, `AgentSessionCreateRequest`, `AgentSessionCreationResult` family                       | EXISTS-AND-USED (WS1-C8) | `src/AgentKit/*.cs`; wired into `AgentEngine.GetAgentAsync`/`CreateSessionAsync` now                                               |
| `AgentEngine.GetAgentAsync` → `ValueTask<AgentResolution>`; `GetAgentsAsync` → `ValueTask<AgentCatalogSnapshot>` | EXISTS-AND-USED (WS1-C8) | matches spec shape (`composition-and-configuration.md:396-403`); implementation still direct, not yet through `AgentEngineRuntime` |
| `AgentEngineRuntime`, `AgentRunPlan`, `IAgentRunPlanCompiler`, `IAgentRunScopeFactory`, `AgentRunScopeLease`     | MISSING                  | –                                                                                                                                  |
| `SessionLaneRegistry`                                                                                            | EXISTS-AND-USED          | `src/AgentKit/SessionLaneRegistry.cs`; only `AgentEngine.cs`; still used by `SendAgentAsync`                                       |
| `IIdentifierGenerator<InputId>`                                                                                  | EXISTS-AND-USED          | registered in `AddAgentKit()` (WS1-C2); `AgentEngine.cs` uses `_inputIds.Create()`                                                 |

### IO package

`IRunEventSink`, `RunEventSinkRegistration`, `RunEventDelivery`,
`IOutputBackpressurePolicy`, `BackpressureDecision` exist in
`src/AgentKit.Abstractions/Results/` (WS1-C4). `DefaultOutputPublisher`,
`AgentIORegistration`, `AddAgentIO`, `AddInputCoordinator<T>(key)` /
`ReplaceInputCoordinator<T>` / `AddOutputPublisher<T>(key)` /
`ReplaceOutputPublisher<T>` / `AddRunEventSink<T>` exist (WS1-C5), all in
`src/AgentKit.IO/`. `RunEventHub` is now DI-constructible: `AgentIORegistration`
registers a scoped factory building it from a new `RunScopeIdentity`
(`src/AgentKit.Abstractions/Composition/RunScopeIdentity.cs`), which
`AgentEngine.SendAgentAsync` populates on `RunScopeState` once the session
address, conversation, and run identity are known — mirroring
`SessionExecutionCapability`'s existing holder pattern. `AgentDefinition` gained
matching optional `InputCoordinatorKey`/`OutputPublisherKey` properties and
`AgentCompositionValidator` validates them when explicitly set (optional
collaborators are not required merely by existing). ~~`AgentRunServicesFactory`
does not yet resolve `IOutputPublisher` into `AgentRunServices`~~ fixed in
WS1-C6: `Compile` now resolves `provider.GetService<IOutputPublisher>()` into
the new `Publisher` slot. Still MISSING: this resolution, and
`IInputCoordinator`'s (`provider.GetService<IInputCoordinator>()`, from WS1-C2),
both remain unkeyed rather than routed through
`AgentDefinition.InputCoordinatorKey`/`OutputPublisherKey` — a known interim gap
for a later chunk, now explicitly the same follow-up for both collaborators.

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
4. ~~Captive dependency: `DefaultInputCoordinator` is singleton but
   `SessionBackedInputQueue` is scoped~~ — fixed by WS1-C2: both are now scoped
   (`input-and-output.md:523`).
5. ~~The engine builds `SessionExecutionCapability` after creating the scope~~ —
   fixed by WS1-C2's `RunScopeState`. `SendAgentAsync` now resolves the session
   and run coordinators directly (via
   `AgentRunServicesFactory.ResolveKeyedOrShared`, made assembly-visible for
   this), builds the capability, and installs it into `RunScopeState` _before_
   calling `AgentRunServicesFactory.Compile` — `Compile` itself may need to
   resolve a scoped `IInputCoordinator` that depends on the capability.
6. ~~`ExecutionLaneId` identity mismatch between engine and loop~~ — fixed by
   WS1-C1's `LoopLaneState`. `AgentEngine.cs:633`'s own best-effort fallback
   release still hard-codes revision 1, which stays correct only because it
   fires after the loop's own release already cleared the lane (see its
   remarks); WS1-C3 does not need to touch it.
7. ~~`PromoteInputAsync` advances `OperationStateRevision`~~ — fixed by WS1-C3:
   `DecideContinuationAsync`/`RunCoreAsync` update
   `LoopLaneState.OperationStateRevision` in place once a commit is observed,
   and `ReleaseLaneAsync` (fixed by WS1-C1) already reads the live value.
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
| `DefaultOutputPublisher` ctor                                                                    | `input-and-output.md:494-501`                | SPEC; `IOutputBackpressurePolicy` SPEC (WS1-C4)    |
| `AddAgentIO` and related registrations                                                           | `input-and-output.md:531-570`                | SPEC; `RunEventSinkRegistration` SPEC (WS1-C4)     |
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
- Landed: `LoopLaneState` threaded through `RunCoreAsync` → `RunTurnAsync` →
  `SettleCompletedAsync`/`ValidateOutputAsync`/`InvokeToolsAsync` →
  `DecideContinuationAsync`, and into `ReleaseLaneAsync`; no public API changed
  (`IAgentLoop.RunAsync` signature is unaffected). Also updated
  `RunAsync_WhenContinuationPolicyContinuesAfterNoToolTurn_RunsAnotherTurn`,
  which had asserted the old fabricated `OperationStateRevision(turn)` behavior.

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
- Landed: `SendAgentAsync` resolves `ISessionCoordinator` and
  `ISessionRunCoordinator` directly (via a newly internal-visibility
  `AgentRunServicesFactory.ResolveKeyedOrShared`) and installs
  `SessionExecutionCapability` into `RunScopeState` **before** calling
  `AgentRunServicesFactory.Compile`, not after — `Compile` itself resolves
  `IInputCoordinator`, which for a session-backed queue needs the capability
  immediately, before session/lane discovery even runs.
  `IO/ServiceExtensions.cs`'s `AddInputCoordinator` is scoped, not singleton,
  matching the scoped `SessionBackedInputQueue` it composes with. Test:
  `AgentTests.SendAsync_WhenSessionBackedInputQueueAndInputCoordinatorAreComposed_ResolvesInputCoordinatorInsideTheRunScope`
  (`tests/AgentKit.Tests` now references `AgentKit.IO` for this one integration
  test). No lifetime assertions in `AgentKit.IO.Tests/ServiceExtensionsTests.cs`
  needed changing; its existing resolution tests pass unchanged under scoped
  registration.

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
- Landed: confirmed — the loop reloads promoted content via
  `services.Session.ReadAsync` rather than reconstructing it from
  `InputPromoted.Promoted`. Extended `InputPromoted` (public, `AgentKit.IO`'s
  only production caller) with `CommittedCursor` and `OperationStateRevision` so
  the loop learns the lane's post-commit state; `SessionInputPromoted` already
  carried both. Discovered and fixed two correctness issues exposed only once
  `PromotedInputContinuationCause` cross-validates against
  `RunContinuationContext` (see the narrative above): the loop's driving
  `OperationId` now reuses the admission's when one exists, and
  `DecideContinuationAsync` builds its continuation context from pre-promotion
  evidence, applying the commit's advanced state afterward. Six tests:
  `RunAsync_WhenInputIsPromotedBeforeFirstModelRequest_IncludesItInTheFirstContextAssemblyRequest`,
  `RunAsync_WhenNoLaneAdmissionIsPresent_NeverAttemptsInputPromotion`,
  `RunAsync_WhenInputIsPromotedAfterTurnCommitted_ForcesContinuationAndIncludesThePromotedMessage`,
  `RunAsync_WhenInputIsPromotedOnlyWhenTheRunWouldOtherwiseFinish_ContinuesInstead`,
  `RunAsync_WhenInputIsPromotedOnTheFinalTurn_StillSettlesWithTheTurnLimit`,
  `RunAsync_WhenPromotionIsRejectedOrConflicted_CompletesNormallyWithoutFailing`.
  Snapshots: Abstractions (`InputPromoted`), Loop (`AgentLoopOptions`).

### WS1-C4: `IRunEventSink`, `RunEventSinkRegistration`, `IOutputBackpressurePolicy`

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `src/AgentKit.Abstractions/Results/IRunEventSink.cs`,
  `RunEventSinkRegistration.cs`, `RunEventDelivery.cs`,
  `IOutputBackpressurePolicy.cs`, `BackpressureDecision.cs`; write the NO-SPEC
  C# blocks into `input-and-output.md` first; tests in
  `AgentKit.Abstractions.Tests/Results/`. Snapshot: Abstractions.
- Open: exact `IOutputBackpressurePolicy` signature.
- Landed: wrote the NO-SPEC C# blocks into `input-and-output.md` (after the
  `IAgentRunStream<TOutput>` interface in the normative output-contracts block)
  plus prose settling the open signature question: `RunEventSinkRegistration`
  takes a sink name, a `RunEventDelivery`, and a fan-out `Order`;
  `IOutputBackpressurePolicy.DecideAsync` takes the delivery, how long the
  attempt has been blocked, and a cancellation token, returning a
  `BackpressureDecision` (`Wait`, `Drop`, `Disconnect`) — a required sink can
  only ever be told to `Wait` (enforced by prose, not yet by a runtime check
  since no caller exists before WS1-C5). One test file,
  `RunEventSinkRegistrationTests.cs`; the four other new types are an interface
  and two enums, matching the existing convention that pure interfaces and enums
  in `Results/` have no dedicated test file. Snapshot: Abstractions.

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
- Landed: `RunScopeState` (not directly visible to `AgentKit.IO`) could not be
  the factory's direct dependency, so a new public
  `AgentKit.RunScopeIdentity(AgentId, SessionId, ConversationId?, RunId)`
  carries the correlation `RunEventHub` needs; `AgentEngine.SendAgentAsync`
  populates it on `RunScopeState.Identity` right after allocating `runId`. Also
  added a matching `AgentIOComponentDefaults` (mirroring
  `AgentLoopComponentDefaults`) and `RequireKeyedOrUnkeyedOptional` in
  `AgentCompositionValidator`, since the cross-cutting rule requires every
  selectable component's key and validator check to land together — the chunk's
  own text only named the output publisher, but `AddAgentIO` accepts both an
  input and an output key, so `AgentDefinition.InputCoordinatorKey` was added
  too. `AddAgentIO` also binds `InputCoordinatorOptions` with this package's
  defaults so it is self-sufficient without a separate
  `AddInputCoordinator(configure)` call. Tests: constructor guards,
  required-sink fault propagation, required-sink backpressure misconfiguration
  (a policy returning anything but `Wait` for a required sink throws),
  best-effort fault isolation, best-effort backpressure drop, delivery order,
  hub forwarding, `CompleteAsync` identity validation/idempotency/conflict, hub
  sealing, plus the registration idempotency/conflict matrix for `AddAgentIO`,
  `AddInputCoordinator<T>`/`ReplaceInputCoordinator<T>`,
  `AddOutputPublisher<T>`/`ReplaceOutputPublisher<T>`, and `AddRunEventSink<T>`.
  `AgentCompositionValidator` end-to-end tests added to
  `AgentEngineBuilderTests`. Snapshots: Abstractions, IO.

### WS1-C6: Loop publishes `RunEvent`s through the publisher

- Depends on: C5. Risk: DENSE-MODIFY `DefaultAgentLoop.ObserveAsync`
  (`:1877-1910`) and commit sites (`:1994`, `:1289`, ~50 lines); rename
  `AgentRunServices.Output` (`IOutputProcessor?`) to `OutputProcessor` and add
  `IOutputPublisher? Publisher` (breaks `AgentRunServicesTests`,
  `DefaultConversationSession.cs:655-659`). Size: M.
- Deliverables: publisher-side sequence allocation; three loop tests with a
  recording publisher. Snapshots: Abstractions, Loop.
- Open: who stamps `Sequence` on immutable `RunEvent` records.
- Landed: `AgentRunServices.Output` renamed to `OutputProcessor`; new
  `Publisher` (`IOutputPublisher?`) property added, resolved unkeyed by
  `AgentRunServicesFactory.Compile` (`provider.GetService<IOutputPublisher>()`,
  the same unkeyed pattern already used for `IInputCoordinator` — routing both
  through `AgentDefinition`'s keys remains the one open interim gap, now
  explicitly the same follow-up for both). `Sequence` is stamped by a new
  `LoopLaneState.AllocateSequence()` (an `Interlocked.Increment` counter):
  `LoopLaneState` was already threaded through every place the loop commits a
  message or observes a model event, so it is the natural single owner of the
  run's event-sequence counter rather than a second object requiring identical
  threading for no other purpose. Two commit sites publish a
  `MessageCommittedEvent` when `services.Publisher` is set: the assistant
  message commit in `SettleCompletedAsync` and the tool-result message commit in
  `InvokeToolsAsync`; both let a publish failure propagate uncaught, since the
  message is already durably committed and a required sink's failure must reach
  the run rather than be silently swallowed. The rejected-tool-calls commit
  inside `SettleRejectedAtTurnLimitAsync` (the turn-limit edge case) is
  deliberately NOT wired to the publisher in this chunk — it has no
  `LoopLaneState` in scope and is a narrow enough edge case to defer. Streamed
  model content is translated to a `ContentDeltaEvent` through a new
  `ObserveAsync` overload (`request`, `services`, `laneState`, `conversationId`,
  `runEvent`, `cancellationToken`) that first delivers to the legacy
  `AgentRunRequest.Observer` exactly as before, then additionally publishes when
  `runEvent is AgentRunModelResponseEvent { ResponseEvent: ModelPartDelta }`;
  the model-response-observer construction gate now also activates when
  `services.Publisher is not null`, not only when
  `request.Observer is not null`. `ObserveDetachedAsync` (tool-call
  started/completed events) is untouched: those events have no defined
  `RunEvent` translation yet, so it keeps calling the original 3-argument
  `ObserveAsync` overload unchanged — this is a deliberate scope cut, not an
  oversight. New `RecordingOutputPublisher` test double
  (`tests/AgentKit.Loop.Tests/RecordingOutputPublisher.cs`) records every
  published event in order, or throws a scripted exception instead. Four new
  loop tests cover: a content-delta event during streaming, a
  `MessageCommittedEvent` for the assistant message, two
  `MessageCommittedEvent`s across a tool-call turn plus a final turn (three
  total, with strictly increasing, distinct sequences), and a required-sink
  failure propagating out of `RunAsync` rather than being swallowed. Full build:
  0 errors, 0 warnings. Tests run: `AgentKit.Loop.Tests` (238 passed),
  `AgentKit.Abstractions.Tests` (6081 passed), `AgentKit.Tests` (403 passed),
  `AgentKit.Conversations.Tests` (266 passed). Snapshots regenerated: only
  `AgentKit.Abstractions.verified.txt` changed (the `AgentRunServices`
  constructor and property rename/addition); `AgentKit.Loop.verified.txt` was
  unaffected because `LoopLaneState` and the new `ObserveAsync` overload are
  internal.

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
- Landed: deleted all 13 `Loop/AgentRun*` records; every production producer
  (`DefaultAgentLoop.cs`, `RunBudget.cs`, `DefaultRunContinuationPolicy.cs`) now
  builds one of the 7 canonical `Results/Run*` outcomes through a new internal
  `src/AgentKit.Loop/RunOutcomes.cs` mapper, so every construction site is one
  reviewable, centrally documented decision instead of scattered `AgentError`
  boilerplate. `rg` for any of the 13 old identifiers across `src/` is empty.
  Full solution: 15,882 tests passing (no regression in count beyond legacy
  dedicated single-type test files deleted alongside their types — see below);
  pushed after `dotnet format`/`--verify-no-changes` clean and the compatibility
  snapshots regenerated (only `AgentKit.Abstractions.verified.txt` changed: the
  13 type removals and `AgentLoopResult.Output`).
  - **Resolved mapping** (`RunOutcomes.cs`'s exact decisions, each with its own
    XML-documented rationale): `Completed→RunSucceeded` (payload moves to the
    envelope: `FinalMessage`/`Output` are no longer on the outcome — see the
    `AgentLoopResult` note below). `Idle→RunIdle`. `Cancelled→RunCancelled`
    wrapping `CancellationReason(AgentError(Code: Cancelled))`.
    `AgentRunOutputRejected→RunPolicyHalted` wrapping
    `PolicyHalt(AgentError(Code: OutputValidationFailed or InvalidConfiguration))`
    — a rejected output candidate is the definition's own output _policy_
    declining the candidate, exactly what `PolicyHalt` exists to describe.
    `AgentRunInvalidState→RunFailed` with the exact-matching
    `AgentErrorCodes.InvalidState`. `AgentRunSessionOperationFailed→RunFailed`
    with `AgentErrorCodes.Unknown` (parity: the old type was also just an
    untyped safe string). `AgentRunAuthorizationUnavailable→RunFailed` with
    `AgentErrorCodes.AuthorizationDenied` (closest existing code; "capture
    unavailable" is effectively a refusal to authorize the operation).
    `AgentRunContextPreparationFailed→RunFailed` with a
    `ContextPreparationFailureKind`-to-`AgentErrorCode` mapping
    (`EmptyHistory→InvalidInput`, `BrokenToolCallCausality`/
    `InvalidRolePartCombination→CorruptState`,
    `InvalidInstructionMessage→InvalidConfiguration`, `Unknown→Unknown`) plus
    the original kind preserved textually in `AgentError.Diagnostics`.
    `AgentRunModelSelectionFailed→RunFailed` with
    `AgentErrorCodes.IncompatibleModel` and only a diagnostic _count_ preserved
    in `Diagnostics` (see fidelity note below).
    `AgentRunFailed(ProviderFailure)→RunFailed` via a
    `ProviderFailureKind`-to-`AgentErrorCode` mapping covering every defined
    kind, with one defensive branch: a `ProviderFailure` whose `Kind` is
    `Cancellation` maps to `RunCancelled` instead of `RunFailed`, since the
    canonical family already has an exact case for that and forcing it through
    `RunFailed` would misrepresent it.
  - **The genuinely hard call — non-budget "limits"**: the chunk's own "Open"
    note asked where `RunLimitFailure` should live for limits the budget
    authority never reserved against. `RunLimitFailure.Limit` is a
    `BudgetLimitFailure`, which always names a real `BudgetScopeId` a
    reservation was evaluated against — `AgentRunRequest.MaxTurns` (a
    loop-configured ceiling, never a budget reservation) and a provider's own
    generation-length ceiling (`NormalizedStopReason.Length`) have no such
    scope, and fabricating one would misrepresent the cause as a budget decision
    it never was. Widening `RunLimitFailure`'s shape to accept non-budget
    evidence (e.g. a new `LimitEvidence` closed union) was considered and
    rejected for this chunk: `RunLimitFailure`/`RunLimitReached` already have
    real, passing dedicated tests
    (`RunLimitFailureTests.cs`/`RunLimitReachedTests.cs`) built entirely around
    `BudgetLimitFailure`, and reshaping them is a distinct, self-contained
    design decision that deserves its own chunk rather than riding along here.
    Resolution actually landed: `AgentRunTurnLimitReached→RunPolicyHalted`
    (`AgentErrorCodes.RequestLimit`, message
    `"The run reached its {n}-turn limit."`) — the caller's own configured turn
    ceiling halting the run is a policy decision, exactly what `PolicyHalt`
    documents. `AgentRunOutputLengthLimitReached→RunFailed`
    (`AgentErrorCodes.TokenLimit`, with `modelRequestId`/`hasPartialOutput`
    preserved in `Diagnostics`) — a provider-reported truncation is not a policy
    the framework configured, so it fits `RunFailed` better than
    `RunPolicyHalted`. Only a genuinely evidence-complete budget rejection
    (`BudgetRejected`, which carries a real `BudgetLimitFailure`) becomes
    `RunLimitReached`; `RunBudget.cs`'s two fallback cases with no such evidence
    (`BudgetHeld`, unsupported reservation outcome) become `RunFailed` via a new
    internal `BudgetExhaustion` record (`Dimension`, `SafeMessage`, optional
    `Failure`, `SideEffectCertainty`) that
    `RunBudget.CountAsync`/`AccountUsageAsync` now return instead of the deleted
    `AgentRunBudgetExhausted`, so the per-tool-call budget-rejection site
    (`BudgetRejectedResultPart`) — which never needed a full outcome, only a
    dimension and message — is unaffected by the outcome-family change at all.
    `SideEffectCertainty` on `BudgetExhaustion`/`RunLimitFailure` is
    `DefinitelyNotPerformed` for `CountAsync` (pre-attempt refusal) and
    `DefinitelyPerformed` for `AccountUsageAsync` (the provider request that
    produced the usage already completed).
  - **`AgentLoopResult` reshape — deliberately partial, not the full spec
    shape**: only `Output` (`ValidatedOutput?`) was added, because
    `DefaultConversationSession` already depended on it via the deleted
    `AgentRunCompleted.Output`. Discovered mid-chunk that the spec's
    `AgentLoopResult.PreviousCursor` is a _required_, non-nullable
    `MessageCursor`, while the loop's real `FinalVersion` is nullable
    specifically for "the run settled before it ever observed the branch"
    (authorization or history-load failure before any read) — reshaping the
    envelope to add `ConversationId`/`Settlement`/`PreviousCursor`/`Usage`
    surfaced this real tension plus `Settlement`'s
    completed-vs-recovery-required derivation needing durable-execution-adjacent
    design work, and touches every terminal return site in `DefaultAgentLoop.cs`
    (~10 call sites), `AgentEngine.cs`'s lane-release fallback, and every
    `AgentLoopResult` construction across the test-doubles list
    (`GatedAgentLoop`, `ScopedRecordingAgentLoop`, `CompositionTestData`,
    `FakeAgentLoop`, plus ~30 in `DefaultConversationSessionTests.cs`). This is
    a distinct, equally large piece of work from unifying the _outcome_ family
    (this chunk's actual name), so it was deliberately left as a follow-up
    rather than folded in under this chunk's already-large budget — added as a
    new explicit prerequisite note for WS1-C9, which is the chunk that actually
    needs to construct `AgentRunFinished<T>` and therefore needs these fields
    for real. To thread `Output` through, `TurnOutcome` (a
    `DefaultAgentLoop`-private struct) gained an `Output` field alongside
    `Outcome`/`Version`, populated only when `DecideContinuationAsync` observes
    `outputDecision is OutputAccepted accepted` at the point it turns a
    `CompleteRun` decision into a settled `TurnOutcome` — the continuation
    policy's own outcome no longer carries the response or output at all
    (`DefaultRunContinuationPolicy` only ever proposes bare
    `RunOutcomes.Completed()`/`.Idle()` now); the loop reads the boundary's
    `OutputDecision` itself in the same call frame instead.
  - **Fidelity trade-offs, explicit and tested**: `AgentError.Diagnostics`
    (`ExtensionData`, canonical-JSON-encoded per entry, matching the existing
    `JsonSerializer.SerializeToUtf8Bytes(value)` convention used across tool
    packages) is the one place richer evidence survives, but only as inspectable
    string values, not the original typed objects — `ModelSelectionDiagnostic[]`
    collapses to a bare `candidateCount`; `OutputRejected`/
    `OutputConfigurationRejected`'s original object (with its `Issues` array) is
    not retained at all, only its `SafeMessage`; a `ProviderFailure`'s
    `ProviderId`/`Kind`/`StatusCode` are individually stashed as diagnostic
    entries, not reconstructable as a `ProviderFailure`. Every test that
    previously asserted on one of these lost fields was rewritten to assert on
    what actually survives (`AgentErrorCode`, `SafeMessage`, or a decoded
    diagnostic entry) with an inline comment explaining the trade-off, rather
    than deleted — `RunContinuationPolicyConformanceTests.cs`'s output-rejection
    case is the clearest example. `AgentError.SideEffectCertainty` is
    `NotApplicable` on every `RunOutcomes`-built error uniformly: none of these
    outcomes report on one specific, potentially-repeatable external effect the
    way a tool invocation does.
  - **Consumers**: `DefaultConversationSession.RunOutcomeKind` (the bounded
    metric/log token) and `DescribeIncompleteOutcome` (the safe user-facing
    description) both now switch on the 7 canonical types, recovering the old
    per-cause distinctions where it mattered by branching on `AgentError.Code`
    inside the `RunPolicyHalted` (`RequestLimit` → "turn limit" phrasing;
    anything else → "output was rejected" phrasing) and `RunLimitReached`
    (always the budget-dimension phrasing) arms — since `RunPolicyHalted` now
    covers two semantically different causes and `RunFailed` covers seven,
    collapsing them to one generic phrase per outer type would have been a real
    UX regression, not just a type-safety one. `EngineDelegationChannel`'s
    `TaskDelegationStatus` switch maps `RunSucceeded→Succeeded`,
    `RunCancelled→Cancelled`, `RunPolicyHalted→Blocked` (turn limit _and_ output
    rejection both read as "the child didn't finish" from a delegator's
    perspective), everything else→`Failed`.
  - **`ArgumentExceptionExtensions.ThrowIfNotSuccessfulRunOutcome`/
    `ThrowIfSuccessfulRunOutcome`** simplified to check only `RunSucceeded`/
    `RunIdle` (the legacy-family branches were dead once the types were
    deleted); the removed `AgentRunCompleted`-specific "must have a complete
    message with run/turn identity" check has no replacement here because
    `RunSucceeded` carries no message to validate —
    `DefaultRunContinuationPolicy` already only proposes it from an
    already-validated committed context, so the invariant is enforced upstream
    instead of redundantly at construction.
  - **Test fallout**: 11 dedicated single-type unit-test files under
    `AgentLoop/` and 5 conformance-binding files under `Messages/` in
    `AgentKit.Abstractions.Tests` were deleted outright (not deprecated) along
    with their types — the canonical family's own dedicated tests
    (`RunSucceededTests.cs`, `RunIdleTests.cs`, `RunCancelledTests.cs`,
    `RunLimitReachedTests.cs`/`RunLimitFailureTests.cs`,
    `RunPolicyHaltedTests.cs`, `RunFailedTests.cs`/`RunFailureTests.cs`,
    `RunDeferredTests.cs`) already existed from earlier planning work and fully
    cover them, so no coverage was lost. `AgentRunFinishedTests.cs`'s
    `Constructor_WhenOutcomeIsLegacy_RequiresExplicitCanonicalMapping` test was
    deleted rather than rewritten: the outcome hierarchy is closed by a
    `private protected` constructor plus a copy-constructor that only accepts
    same-concrete-type originals (proven separately by
    `Results/AgentRunOutcomeTests.cs`'s `ForeignVariant` test), so constructing
    a non-canonical `AgentRunOutcome` to exercise that branch is no longer
    possible from outside the assembly — the scenario itself is gone, not just
    untested. `DefaultAgentLoopTests.cs`'s ~150 references were updated
    mechanically (outer-type swap, then per-property-access-chain fixes guided
    by compiler errors) and its affected test method names were renamed to match
    the new outcome names (e.g. `...ReturnsAgentRunTurnLimitReached` →
    `...ReturnsRunPolicyHalted`). New tests added:
    `Constructor_WhenOutputIsSupplied_RoundTripsProperty`/
    `Constructor_WhenOutputIsOmitted_DefaultsToNull` on
    `AgentLoopResultTests.cs` for the new `Output` field.

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
- Landed: renamed loop `AgentRunRequest` to `AgentLoopRunRequest` first (own
  commit, 22 files, pure rename, zero behavior change) to free the name. Added
  `SessionCreationFailureKind`/`SessionCreationFailure` to
  `AgentKit.Abstractions/Sessions/` after writing their NO-SPEC block into
  `composition-and-configuration.md` (a `Kind`-plus-`SafeMessage` shape matching
  `ContextPreparationFailure`/`OutputSchemaConfigurationFailure` rather than a
  new failure-evidence pattern). Added the facade-level
  `AgentResolution`/`ResolvedAgent`/`AgentNotFound`/`InvalidAgent`,
  `AgentSessionCreateRequest`/`AgentSessionCreationResult`/
  `AgentSessionCreated`/`AgentSessionCreationFailed`, and the new facade
  `AgentRunRequest` to `src/AgentKit/` (the facade project), not
  `AgentKit.Abstractions`: `ResolvedAgent` wraps the concrete `Agent` class,
  which only exists in the facade assembly, and `AgentRunOptions` (the existing
  analog) already lived in the facade project for the same reason —
  `AgentKit.Abstractions` stays provider-neutral and facade-agnostic.
  `AgentResolution` is deliberately a distinct closed hierarchy from the
  existing catalog-level `AgentDefinitionResolution`: that family resolves an
  identity to a definition, before any facade handle exists; this one resolves
  an identity to a live, engine-bound `Agent` handle.
  - `AgentRunOptions` reshaped to exactly
    `(int? MaxTurns, TimeSpan? AttemptTimeout)`, dropping
    `SessionId`/`BranchId`/`ExecutionIdentity`.
    `Agent.RunAsync`/`AgentEngine.RunAgentAsync` (the lane-bypassing path)
    gained those three as explicit leading parameters instead
    (`RunAsync(sessionId, branchId, identity, options = null, ct)`), matching
    the shape the eventual `RunAsync<TOutput>` will need — this is a pure
    parameter-regrouping refactor with identical behavior, not the
    "lane-bypassing `RunAgentAsync` removed" step, which stays WS1-C9's job per
    its own deliverables list. `options` is now optional (`null` uses the
    definition's defaults outright) since it no longer carries anything
    mandatory.
  - `GetAgentAsync`/`GetAgentsAsync` reshaped to return `AgentResolution`/
    `AgentCatalogSnapshot` now, without needing `AgentEngineRuntime`: both
    methods' current bodies only ever needed `IAgentDefinitionCatalog` directly,
    so the return-type contract change lands now and C9 only needs to move the
    _implementation_ into the runtime later, not change the signature again.
    `InvalidAgentDefinition` no longer becomes a thrown
    `InvalidOperationException` from `GetAgentAsync`; it becomes a typed
    `InvalidAgent` result, consistent with `AgentNotFound` already being a typed
    result rather than `null`.
  - `AgentEngine.CreateSessionAsync(AgentSessionCreateRequest, CT)` is a new,
    fully independent admission path (not layered on `SendAgentAsync`): it
    resolves the requested `AgentId` fresh against the current catalog (since a
    caller may hold only the identity, not an already-pinned `Agent` handle),
    captures authorization, and calls `ISessionCoordinator.CreateAsync`
    directly, translating every failure into `AgentSessionCreationFailed` with
    the matching `SessionCreationFailureKind` rather than throwing — including
    catching the authorization helper's own `AgentAdmissionRejectedException`
    and translating it to `SessionCreationFailureKind.AuthorizationUnavailable`
    rather than reusing that exception type at this new, non-throwing boundary.
    `Agent.CreateSessionAsync` is a thin convenience wrapper forwarding to it
    with `Id` already filled in, matching `Agent.SendAsync`'s existing
    delegation pattern.
  - Test fallout: ~76 `GetAgentAsync` call sites and ~12 `GetAgentsAsync` call
    sites across `AgentKit.Tests`, `AgentKit.Simple.Tests`, and
    `QuickStart.Tests` updated (`(await engine.GetAgentAsync(...))!` → a new
    `CompositionTestData.RequireResolved()` extension in `AgentKit.Tests`, or an
    inline `.ShouldBeOfType<ResolvedAgent>().Agent` in `AgentKit.Simple.Tests`,
    which has no shared composition-data helper of its own;
    `GetAgentsAsync(...)` results gained a `.Definitions` accessor at every call
    site). ~44 `Agent.RunAsync(CompositionTestData.RunOptions(...), CT)` call
    sites updated to pass `CompositionTestData.SessionId`/`BranchId`/
    `Identity()` as new leading arguments.
    `AgentEngineTests .GetAgentAsync_WhenCatalogReportsAnInvalidDefinition_...`
    rewritten from asserting a thrown `InvalidOperationException` to asserting a
    typed `InvalidAgent` result.
    `AgentEngineTests .RunAsync_WhenOptionsIsNull_ThrowsArgumentNullException`
    rewritten to `..._UsesTheDefinitionsDefaults`, since `options: null` is now
    valid input, not an error. `AgentRunOptionsTests.cs` rewritten from scratch:
    its 3 original tests all exercised the now-removed `Identity`/`SessionId`/
    `BranchId` fields; replaced with 4 tests covering the narrow
    `MaxTurns`/`AttemptTimeout` shape's own validation and round-trip.
  - Full solution: 15,885 tests passing (up from 15,882: net +3 new/replaced
    tests in `AgentRunOptionsTests.cs` and `AgentEngineTests.cs`). Snapshots
    regenerated: `AgentKit.Abstractions.verified.txt` (the two new session
    types) and `AgentKit.verified.txt` (every new facade type, the
    `AgentRunOptions`/`Agent.RunAsync`/`AgentEngine.RunAgentAsync`/
    `GetAgentAsync`/`GetAgentsAsync`/`CreateSessionAsync` signature changes).
  - Deferred, explicitly not touched in this chunk: the "Current admission
    surface" prose (`composition-and-configuration.md:445-473`) describing
    `SendAsync`'s reduced flow — that flow's behavior did not change in C8 (only
    new, separately-reachable types and the bypass path's parameter shape did),
    so rewriting it now would describe C9's not-yet-landed `AgentEngineRuntime`
    wiring prematurely; C9 is the right chunk to update it alongside the runtime
    move.

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
- **Landed (prerequisite: usage accounting)**: `AgentRunFinished<T>` requires a
  real, non-null `RunUsage`, and nothing in the loop produced one before this
  chunk — `RunUsage`/`UsageAccountingEntry` existed only as unwired Abstractions
  types. Added `src/AgentKit.Loop/UsageAccounting.cs` (internal, pure): maps one
  model response's `ModelUsage` into a `UsageAccountingEntry`, reusing
  `RunBudget.AccountUsageAsync`'s dimension mapping for input/output/ reasoning
  tokens and cost, but as a historical record rather than a reservation — it
  also retains cached-read tokens (`BudgetDimensions .CachedReadTokens`; no
  budget dimension reserves them today) and accepts any reported cost currency
  (`new BudgetUnit(currency.ToLowerInvariant())`) rather than only `"usd"`,
  since accounting makes no enforcement decision. Returns `null` when the
  provider reports nothing at all (`ModelUsageReportState.NotReported` or every
  counter absent), matching `RunBudget`'s own skip-unreported convention.
  `LoopLaneState` (already threaded through every turn) gained a mutable `Usage`
  property, seeded `new RunUsage(runId, [])` at construction (so
  `LoopLaneState`'s constructor now also takes `RunId`) and updated via
  `laneState.Usage = laneState.Usage.Apply(entry)` at the same call site as
  `RunBudget.AccountUsageAsync` in `SettleCompletedAsync` — usage is accounted
  for the run's own frozen projection unconditionally, regardless of whether
  budget limits are configured, since accounting and enforcement are separate
  concerns over the same report. Matching `RunBudget`'s own current scope, only
  the clean-completion path accounts usage today; `SettleInterruptedAsync`'s
  responses are not yet accounted either way (a pre-existing gap this chunk does
  not widen or fix). `DefaultAgentLoop` gained an
  `IIdentifierGenerator<UsageEntryId>? usageEntryIds = null` constructor
  parameter (additive, defaults to a `GuidIdentifierGenerator<UsageEntryId>`
  matching every other optional identifier generator on this type — not a
  contract break). `AgentLoopResult` gained two new required constructor
  parameters, `RunUsage usage` and `RunSettlementOutcome settlement` (a real
  contract break for this loop-internal type, expected test fallout below):
  **not** `ConversationId` or `PreviousCursor` as the chunk's own
  verified-current-state table originally listed. Those two belong at the
  facade, not the loop: `AgentEngineRuntime` already has (or, from admission,
  can cheaply obtain) `ConversationId` from `SessionDescriptor.ConversationId`
  and `PreviousCursor` from the branch-tip read it performs before invoking the
  loop (`LoadBranchTipAsync`'s `SessionVersion`/`SessionBranchCursor` in today's
  `SendAgentAsync`), so building `AgentRunFinished<T>.PreviousCursor` from
  admission-time evidence resolves the nullability tension WS1-C7's Landed note
  flagged (spec's `PreviousCursor` is non-nullable;
  `AgentLoopResult.FinalVersion` is genuinely nullable for "settled before
  observing history") without inventing a fabricated cursor for that case — a
  run that never reached history load was never accepted, so it can never reach
  `AgentRunFinished<T>` construction at all; it settles as `AgentRunRejected<T>`
  instead. `Settlement` is unconditionally `RunSettlementCompleted` for now: the
  loop only ever returns `AgentLoopResult` after its own bounded settlement
  attempt actually finished, so this is truthful today, not a placeholder; a
  future recovery boundary (durable abort/crash resume, no chunk yet) is what
  would ever produce `RunSettlementRecoveryRequired`. Test fallout:
  `AgentLoopResultTests` (4 new tests for the two added parameters'
  null/mismatched-run-id validation), 5 dedicated `UsageAccountingTests`, 2 new
  `DefaultAgentLoopTests` (usage accumulates on success; stays empty when
  unreported), plus mechanical constructor-argument additions in
  `CompositionTestData.cs`, `GatedAgentLoop.cs`, `ScopedRecordingAgentLoop.cs`
  (`AgentKit.Tests`), `FakeAgentLoop.cs` and 11 call sites in
  `DefaultConversationSessionTests.cs` (`AgentKit.Conversations.Tests`) — all
  additive default evidence (`new RunUsage(request.RunId, [])`,
  `new RunSettlementCompleted()`), no behavioral assertions changed. Snapshots:
  Abstractions, Loop. Full solution: 16,206 passing before this piece; +9 net
  new tests.

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
