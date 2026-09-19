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

- [ ] WS13-C1 identity keys and value types
- [ ] WS13-C2 `AgentGoal`, `GoalAttempt`, `GoalTransition`, delegation records
- [ ] WS13-C3 store, coordinator, join, event, policy, dispatch contracts
- [ ] WS13-C4 goal-store conformance suite
- [ ] WS13-C5 `AgentKit.Goals.InMemory`
- [ ] WS13-C6 session-backed projection
- [ ] WS13-C7 `AgentKit.Goals.Sqlite` and `.Json`
- [ ] WS13-C8 `AgentKit.Goals` runtime
- [ ] WS13-C9 join strategies
- [ ] WS13-C10 local dispatcher and `AgentKit.Goals.Hosting` worker
- [ ] WS13-C11 `Tools.Task` migration and communication
- [ ] WS13-C12 definition key, validator, Simple, documentation

## Verified current state

| Item                                                                                                                                                                                                                                                   | State           | Evidence                                                                                                                                   |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| every goal contract and value (`AgentGoal`, `GoalAttempt`, `GoalTransition`, canonical `DelegationRequest/Result`, `IGoalStore`, coordinators, target provider/catalog/selector, policy pipeline, dispatcher, join strategy, event sinks, keys, enums) | MISSING         | `src/AgentKit.Abstractions/Goals/` does not exist                                                                                          |
| `GoalId`, `GoalAttemptId`, `GoalProfileKey`, `DelegationId`, `VersionToken`, `IdempotencyKey`                                                                                                                                                          | EXISTS          | `Abstractions/Identity/`                                                                                                                   |
| `TaskDelegation*` adapter contracts (10 files)                                                                                                                                                                                                         | EXISTS-AND-USED | `Abstractions/Delegation/`; `Goals/DefaultTaskDelegationBroker.cs`, `Tools.Task/TaskTool.cs:27,141`, `AgentKit/EngineDelegationChannel.cs` |
| `DefaultTaskDelegationBroker`                                                                                                                                                                                                                          | EXISTS-AND-USED | `Goals/DefaultTaskDelegationBroker.cs:7-141`; `AddAgentDelegation`                                                                         |
| `EngineDelegationChannel` runs children inline                                                                                                                                                                                                         | CONFIRMED       | lazy engine resolve at `:80`, `target.SendAsync(...)` inside the parent tool call at `:94-96`; violates `goals-and-delegation.md:317-323`  |
| `AgentKit.Goals.Hosting`, communication envelope, goal session entries                                                                                                                                                                                 | MISSING         | –                                                                                                                                          |
| observability                                                                                                                                                                                                                                          | minimal         | `task.delegation.dispatch` plus two metrics                                                                                                |

Test doubles: `ITaskDelegationChannel` 1
(`Goals.Tests/DelegationTestDoubles.cs:69`), `ITaskDelegationBroker` 1
(`Tools.Task.Tests/TestDoubles.cs:6`); `EngineDelegationChannelTests.cs` (248
lines); none break if `TaskDelegation*` is retained.

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

### WS13-C2: Core records

- Depends on: C1. Risk: ADDITIVE (keeps `TaskDelegation*`). Size: M.
- Deliverables: `AgentGoal`, `GoalAttempt`, `GoalTransition` (with a static
  validity table), `DelegationRequest`, `DelegationResult`,
  `DelegationRejected`, `DelegationChildResult`; transition matrix tests.

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

### WS13-C4: Goal-store conformance suite

- Depends on: C3. Risk: ADDITIVE. Size: M.
- Deliverables: idempotent create, `ExpectedVersion` CAS, transition replay,
  children ordinal paging, grant denial before write, cross-agent/session
  rejection.

### WS13-C5: `AgentKit.Goals.InMemory`

- Depends on: C4. Risk: ADDITIVE new project. Size: M.
- Deliverables: `InMemoryGoalStore`, enforcement receipt,
  `AddInMemoryGoalStore(GoalStoreKey)`; suite green.

### WS13-C6: Session-backed projection

- Depends on: C4, WS1. Risk: ADDITIVE with new session entry kinds. Size: M.
- Deliverables: `GoalCreatedSessionEntry`, `GoalTransitionSessionEntry`;
  `SessionBackedGoalStore` in `AgentKit.Goals` (Goals owns its
  `ISessionEntryCodec`); suite over `InMemorySessionStore`; replay
  reconstruction; codec conformance fixture.

### WS13-C7: `AgentKit.Goals.Sqlite` and `.Json`

- Depends on: C4. Risk: ADDITIVE new projects. Size: L.

### WS13-C8: `AgentKit.Goals` runtime

- Depends on: C3, C5. Risk: DENSE-MODIFY `Goals/ServiceExtensions.cs`. Size: L.
- Deliverables: options, profiles, snapshot, registration,
  `DefaultGoalCoordinator`, `DelegationCoordinator`,
  `LocalAgentDelegationTargetProvider` over `IAgentDefinitionCatalog`, target
  catalog and selector, policy pipeline, `DenyUnlessAuthorizedDelegationPolicy`,
  `DefaultGoalBudgetManager` (WS11), event dispatcher, selectors; `goal.*` and
  `delegation.*` activity names; `AddAgentDelegation` retained.

### WS13-C9: Join strategies

- Depends on: C8. Risk: ADDITIVE. Size: M.
- Deliverables: all-results, ordinal-first-success, fastest-valid-success
  (recorded winner), quorum, best-effort; matrix tests; no strategy reads task
  completion order.

### WS13-C10: Local dispatcher and hosting worker

- Depends on: C8, WS1. Risk: ADDITIVE new leaf plus DENSE-MODIFY
  `EngineDelegationChannel.cs:69-136`. Size: L.
- Deliverables: `LocalDelegationDispatcher` commits child-admission intent;
  `AgentKit.Goals.Hosting` with `GoalDelegationWorker : BackgroundService`
  draining intents through the public agent surface with idempotent replay;
  `EngineDelegationChannel` moves to the hosting leaf with an obsolete forwarder
  in the facade; worker drain and process-loss tests.
- Done when: a child never runs inline in the parent call; a one-worker host
  parks the parent.

### WS13-C11: `Tools.Task` migration and communication

- Depends on: C10. Risk: DENSE-MODIFY `TaskTool.cs:83-150`. Size: M.
- Deliverables: `TaskTool` builds a canonical `DelegationRequest` through
  `IDelegationCoordinator`; agent-to-agent messages are admitted input with
  sender, recipient, goal, attempt, idempotency; retried task messages create no
  duplicate child.

### WS13-C12: Definition key, validator, Simple, documentation

- Depends on: C8–C11. Risk: DENSE-MODIFY definition equality, validator, Simple.
  Size: M.
- Deliverables: `GoalProfileKey? GoalProfile`; validator requires coordinator,
  selectors, store and dispatcher keys, join strategies, budget manager, limits;
  `WithDelegation` registers the runtime, profile, InMemory store, and worker;
  architecture and use-case updates; skill.

## Totals

S 1, M 8, L 3. Confidence high on state; medium on C10 (facade relocation and
WS1 dependency).
