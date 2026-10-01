# WS13: Goals, joins, worker hosting

Goal: goals, attempts, and transitions are durable domain state in a keyed
`IGoalStore` (InMemory, Sqlite, Json, plus a session-backed projection); a
delegation coordinator authorizes, records intent, and dispatches through a
policy pipeline; children run in separately admitted sessions drained by a
hosted worker instead of inline inside the parent's tool call; joins are
recorded strategies; and agent-to-agent communication goes through input
admission.

Owning documents:
[Goals and delegation](../architecture/goals-and-delegation.md),
[Goals and multi-agent delegation](../concepts/goals-and-multi-agent-delegation.md).

## Progress

- [x] WS13-C1 identity keys and value types
- [x] WS13-C2 `AgentGoal`, `GoalAttempt`, `GoalTransition`, delegation records
- [x] WS13-C3 store, coordinator, join, event, policy, dispatch contracts
- [x] WS13-C4 goal-store conformance suite
- [x] WS13-C5 `AgentKit.Goals.InMemory`
- [x] WS13-C6 session-backed projection
- [x] WS13-C7 `AgentKit.Goals.Sqlite` and `.Json`
- [x] WS13-C8 `AgentKit.Goals` runtime
- [x] WS13-C9 join strategies
- [x] WS13-C10 local dispatcher and `AgentKit.Goals.Hosting` worker
- [x] WS13-C11 `Tools.Task` migration and communication
- [x] WS13-C12 definition key, validator, Simple, documentation

## Verified current state

| Item                                                                                                                                               | State   | Evidence                                                                                                                                  |
| -------------------------------------------------------------------------------------------------------------------------------------------------- | ------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Goal contracts and values, keys, enums, stores, coordinators, selectors, policy pipeline, dispatcher, join strategy, events, parking, child runner | LANDED  | `src/AgentKit.Abstractions/Goals/`, `Identity/Goal*Key.cs`                                                                                |
| Stores: InMemory, Json, Sqlite, session-backed, one shared conformance suite                                                                       | LANDED  | `src/AgentKit.Goals.{InMemory,Json,Sqlite}`, `Goals/SessionBackedGoalStore.cs`, `tests/AgentKit.Conformance/GoalStoreConformanceTests.cs` |
| Goals runtime (profiles, coordinators, selectors, pipeline, budget manager, events, joins, local dispatcher)                                       | LANDED  | `src/AgentKit.Goals/`                                                                                                                     |
| `AgentKit.Goals.Hosting` (worker, slots and parking, engine child runner, message channel)                                                         | LANDED  | `src/AgentKit.Goals.Hosting/`                                                                                                             |
| `TaskDelegation*` contracts, `DefaultTaskDelegationBroker`, `AddAgentDelegation`, `EngineDelegationChannel`, old task-delegation observability     | REMOVED | deleted; `TaskTool` uses `IDelegationCoordinator`                                                                                         |
| `GoalProfileKey? GoalProfile` on `AgentOptionalCapabilitySelection`                                                                                | EXISTED | `Abstractions/Composition/AgentOptionalCapabilitySelection.cs`                                                                            |
| Goals composition validator                                                                                                                        | LANDED  | `src/AgentKit/GoalsCompositionValidator.cs`, wired from `AgentCompositionValidator`                                                       |
| Observability                                                                                                                                      | LANDED  | `goal.*`, `delegation.*`, `agent.message.send` activities and bounded metrics in `AgentKit.Observability`                                 |

## Hidden prerequisites

1. `HookDispatchContext` (WS2) on `IDelegationCoordinator`; omit until then.
2. The facade may not reference `AgentKit.Goals`; the worker and any
   coordinator-backed channel live in `AgentKit.Goals.Hosting`.
3. WS11 `IBudgetScope`/`BudgetExecutionCapability` for child reservations.
4. WS1 admission and lane semantics for separately admitted child sessions.
5. A new `SessionEntry` subtype and codec for the session-backed projection must
   join the codec conformance suite.
6. `SecurityOperationKind.Delegation` already exists.

## Spec coverage

| Contract                                                                                                                                                                                                 | Spec                                  |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------- |
| identity keys                                                                                                                                                                                            | `goals-and-delegation.md:42-60`       |
| `AgentGoal`, `GoalAttempt`, `GoalTransition`, delegation records                                                                                                                                         | `:65-156`                             |
| `IGoalStore`, `IGoalStoreSelector`, `IGoalCoordinator`                                                                                                                                                   | `:168-213`                            |
| target provider/catalog/selector, policy pipeline, dispatcher, delegation coordinator, join strategy, event sink                                                                                         | `:223-308`                            |
| `DelegationCoordinator` ctor; options; DI                                                                                                                                                                | `:342-360,377-530`                    |
| `GoalStatus` members                                                                                                                                                                                     | concept prose `:26-28`; NO-SPEC block |
| `GoalDefinition`, budgets, criteria, scope, results, rejection, actor, reason, request/result bodies, join request/decision, `GoalEvent`, `AuthorizedDelegation`, selectors, `IGoalBudgetManager` bodies | NO-SPEC                               |
| hosting worker; communication envelope                                                                                                                                                                   | prose `:317-331,615-618`; NO-SPEC     |

## Chunks

### WS13-C1: Identity keys and value types

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `Identity/GoalStoreKey`, `DelegationDispatcherKey`,
  `GoalJoinStrategyKey`, `GoalProfileVersion`; `Goals/GoalStatus`,
  `GoalAttemptStatus`, `TransitionActor`, `GoalTransitionReason`,
  `DelegationStatus`, `DelegationCancellationMode`, `DelegationFailureMode`,
  `GoalDefinition`, `GoalBudget`, `GoalBudgetReservation` (wraps the budgets
  reservation), `GoalBudgetUsage`, `GoalOutcomeReference`, `AcceptanceCriteria`,
  `DelegationScope`, `StructuredGoalResult`, `EvidenceReference`,
  `DelegationRejection`; identity conformance tests. Snapshot: Abstractions.

- Landed: four identity keys, eight enums, and the value types with validating
  constructors and tests in `Abstractions.Tests`.

### WS13-C2: Core records

- Depends on: C1. Risk: ADDITIVE (keeps `TaskDelegation*`). Size: M.
- Deliverables: `AgentGoal`, `GoalAttempt`, `GoalTransition` (with a static
  validity table), `DelegationRequest`, `DelegationResult`,
  `DelegationRejected`, `DelegationChildResult`; transition matrix tests.

- Landed: `AgentGoal` (with `WithState`), `GoalAttempt`, `GoalTransition` with
  the validity table, `DelegationRequest`, and the result family; full
  transition-matrix tests. `GoalRecord` additionally carries sequence, child
  ordinal, settled sequence, and the stored delegation.

### WS13-C3: Contracts

- Depends on: C2. Risk: ADDITIVE. Size: M.
- Deliverables: `IGoalStore` and descriptor, create/load/transition/children
  requests and results, `IGoalStoreSelector`, `IGoalCoordinator`,
  `IDelegationTargetProvider`, discovery request, target snapshot and catalog,
  `IDelegationTargetSelector`, `IDelegationPolicy`, pipeline, context, decision,
  registration, `IDelegationDispatcher` and descriptor, `AuthorizedDelegation`,
  `IDelegationDispatcherSelector`, `IDelegationCoordinator`, `IGoalJoinStrategy`
  and selector, join request and decision, `GoalJoinStrategyKeys`,
  `IGoalBudgetManager`, event sink, dispatcher, `GoalEvent`, registration. Store
  requests carry `SecurityGrant`.

- Landed: every listed contract plus `GoalSecurityBinding`,
  `DelegationSecurityBinding`, `RunRootGoal`, `GoalProfileSnapshot`,
  `DelegationIntent`, `IDelegationIntentSignal`, `IDelegationWaitParking`,
  `IDelegationChildRunner`, and the agent-message contracts. Deviations (command
  versus request types, attempt changes riding on transitions,
  `ReadChildrenAsync`/`ReadIntentsAsync`) are recorded in
  [the architecture document](../architecture/goals-and-delegation.md#implementation-notes-and-recorded-deviations).

### WS13-C4: Goal-store conformance suite

- Depends on: C3. Risk: ADDITIVE. Size: M.
- Deliverables: idempotent create, `ExpectedVersion` CAS, transition replay,
  children ordinal paging, grant denial before write, cross-agent/session
  rejection.

- Landed: `GoalStoreConformanceTests` with `IGoalStoreConformanceFixture`; run
  by all four stores. Session-backed conformance accepts scope-mismatch or
  not-found for a cross-scope read.

### WS13-C5: `AgentKit.Goals.InMemory`

- Depends on: C4. Risk: ADDITIVE new project. Size: M.
- Deliverables: `InMemoryGoalStore`, enforcement receipt,
  `AddInMemoryGoalStore(GoalStoreKey)`; suite green.

- Landed: `AgentKit.Goals.InMemory` with `AddInMemoryGoalStore`; intent
  discovery is refused unless the host names the scanner in
  `InMemoryGoalStoreOptions.AuthorizedIntentScanners`.

### WS13-C6: Session-backed projection

- Depends on: C4, WS1. Risk: ADDITIVE with new session entry kinds. Size: M.
- Deliverables: `GoalCreatedSessionEntry`, `GoalTransitionSessionEntry`;
  `SessionBackedGoalStore` in `AgentKit.Goals` (Goals owns its
  `ISessionEntryCodec`); suite over `InMemorySessionStore`; replay
  reconstruction; codec conformance fixture.

- Landed: `SessionBackedGoalStore`, the two session entries with codecs and
  codec fixtures, replay reconstruction. It persists no delegation
  authorization, so it reports no durability and no intent discovery.

### WS13-C7: `AgentKit.Goals.Sqlite` and `.Json`

- Depends on: C4. Risk: ADDITIVE new projects. Size: L.

- Landed: `AgentKit.Goals.Json` (single-writer, `store.json` manifest plus
  `goals.jsonl`) and `AgentKit.Goals.Sqlite`, each with READMEs, slnx entries,
  and conformance plus leaf tests. All three adapters compile one pure reducer
  and planner from source-only `AgentKit.Goals.Storage.Shared` (and `.Durable`
  for the durable leaves).

### WS13-C8: `AgentKit.Goals` runtime

- Depends on: C3, C5. Risk: DENSE-MODIFY `Goals/ServiceExtensions.cs`. Size: L.
- Deliverables: options, profiles, snapshot, registration,
  `DefaultGoalCoordinator`, `DelegationCoordinator`,
  `LocalAgentDelegationTargetProvider` over `IAgentDefinitionCatalog`, target
  catalog and selector, policy pipeline, `DenyUnlessAuthorizedDelegationPolicy`,
  `DefaultGoalBudgetManager` (WS11), event dispatcher, selectors; `goal.*` and
  `delegation.*` activity names; `AddAgentDelegation` retained.

- Landed: options, profile registry and catalog, registration surface with
  `Replace*` methods, `DefaultGoalCoordinator`, `DelegationCoordinator`,
  `LocalAgentDelegationTargetProvider`, target catalog and selector, policy
  pipeline with ordering and narrowing validation,
  `DenyUnlessAuthorizedDelegationPolicy`, `DefaultGoalBudgetManager`, event
  dispatcher, selectors, and the `goal.*`/`delegation.*` observability.
  `AddAgentDelegation` was removed in C11 rather than retained.

### WS13-C9: Join strategies

- Depends on: C8. Risk: ADDITIVE. Size: M.
- Deliverables: all-results, ordinal-first-success, fastest-valid-success
  (recorded winner), quorum, best-effort; matrix tests; no strategy reads task
  completion order.

- Landed: the five strategies, each registered as a keyed singleton, with a
  matrix test suite including out-of-order settlement and replay determinism.

### WS13-C10: Local dispatcher and hosting worker

- Depends on: C8, WS1. Risk: ADDITIVE new leaf plus DENSE-MODIFY
  `EngineDelegationChannel.cs:69-136`. Size: L.
- Deliverables: `LocalDelegationDispatcher` commits child-admission intent;
  `AgentKit.Goals.Hosting` with `GoalDelegationWorker : BackgroundService`
  draining intents through the public agent surface with idempotent replay;
  `EngineDelegationChannel` moves to the hosting leaf and every caller is
  updated, with no forwarder left in the facade; worker drain and process-loss
  tests.
- Done when: a child never runs inline in the parent call; a one-worker host
  parks the parent.

- Landed: `LocalDelegationDispatcher` (idempotent ready-intent commit, own grant
  validation), `GoalDelegationWorker` with startup and periodic durable scans,
  bounded slots with session-keyed parking, claim-before-run settlement,
  stale-attempt recovery, deadline and stop handling, lazy start for standalone
  engines, and `EngineDelegationChildRunner`. `EngineDelegationChannel` was
  deleted from the facade rather than moved, because its contract
  (`ITaskDelegationChannel`) no longer exists; the runner replaces it.

### WS13-C11: `Tools.Task` migration and communication

- Depends on: C10. Risk: DENSE-MODIFY `TaskTool.cs:83-150`. Size: M.
- Deliverables: `TaskTool` builds a canonical `DelegationRequest` through
  `IDelegationCoordinator`; agent-to-agent messages are admitted input with
  sender, recipient, goal, attempt, idempotency; retried task messages create no
  duplicate child.

- Landed: `TaskTool` over `IDelegationCoordinator` (idempotency key
  `task:{toolCallId}`, fails closed without a configured goal profile);
  `TaskDelegation*`, the broker, `AddAgentDelegation`,
  `EngineDelegationChannel`, and their logs, metrics, tests, and docs removed;
  `IAgentMessageChannel` with `EngineAgentMessageChannel` admitting
  deterministic-identity steering or follow-up input. No model-facing messaging
  tool was added.

### WS13-C12: Definition key, validator, Simple, documentation

- Depends on: C8–C11. Risk: DENSE-MODIFY definition equality, validator, Simple.
  Size: M.
- Deliverables: `GoalProfileKey? GoalProfile`; validator requires coordinator,
  selectors, store and dispatcher keys, join strategies, budget manager, limits;
  `WithDelegation` registers the runtime, profile, InMemory store, and worker;
  architecture and use-case updates; skill.

- Landed: `GoalProfileKey? GoalProfile` already existed on the capability
  selection; `GoalsCompositionValidator` wired into composition validation;
  `AgentKit.Simple.WithDelegation` registers the runtime, one profile, the
  in-memory store, the local dispatcher, the hosted worker, the task tool, and
  the message channel and selects the profile on every definition; README,
  architecture, use-case, profile, and skill documents updated.

## Totals

S 1, M 8, L 3. Confidence high on state; medium on C10 (facade relocation and
WS1 dependency).
