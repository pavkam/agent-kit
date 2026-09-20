# WS12: Durable execution

Goal: a neutral `AgentKit.Durability` runtime (options, profiles, backend
catalog and selector, runtime lease, recovery policy, fenced journal, event
dispatcher, coordinator) over journal and lease-manager adapters in InMemory,
Sqlite, and Json, with grant-consuming and audited journal writes, and consumers
checkpointing model requests, tool calls, admission, settlement, approval waits,
and compaction activation.

Owning documents: [Durable execution](../architecture/durable-execution.md),
[Durable execution and recovery](../concepts/durable-execution-and-recovery.md).

## Progress

- [ ] WS12-C1 `RecoveryEvidence.RecordedResult`/`NotBefore`,
      `DurableOperationWaiting`
- [ ] WS12-C2 `AuthorizedDurableRequest<T>` and `RecordWaitingAsync`
- [ ] WS12-C3 backend, runtime, coordinator, event contracts
- [ ] WS12-C4 journal and lease-manager conformance suites
- [ ] WS12-C5 InMemory keyed, grant-consuming, audited
- [ ] WS12-C6 `AgentKit.Durability` runtime package
- [ ] WS12-C7 `DefaultRecoveryPolicy` and fenced journal decorator
- [ ] WS12-C8 `DurableExecutionCoordinator`
- [ ] WS12-C9 `AgentKit.Durability.Sqlite`
- [ ] WS12-C10 `AgentKit.Durability.Json`
- [ ] WS12-C11 definition key and validator
- [ ] WS12-C12a loop checkpoints
- [ ] WS12-C12b engine, settlement, approval, compaction checkpoints
- [ ] WS12-C13 Simple `WithDurability` and documentation

## Verified current state

| Item                                                                                                                                                                                                                                                                                                                             | State                               | Evidence                                                                                                                           |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| `AgentKit.Durability` package                                                                                                                                                                                                                                                                                                    | MISSING                             | only `AgentKit.Durability.InMemory`                                                                                                |
| `IDurableExecutionCoordinator`, `IDurableExecutionBackend`, backend catalog/selector, runtime selector/lease, dispatch and reconciliation types, event sink/dispatcher, `DurableExecutionEvent`, `UnknownEffectRecoveryMode`, `DurableCheckpointMode`, `DurabilityUnavailable`, `RecoveryIncompatible`, `OperatorActionRequired` | MISSING (22 types)                  | –                                                                                                                                  |
| `IRecoveryPolicy`                                                                                                                                                                                                                                                                                                                | EXISTS-UNWIRED                      | `Abstractions/Durability/IRecoveryPolicy.cs:30-63`; zero impls                                                                     |
| `IDurableOperationJournal`                                                                                                                                                                                                                                                                                                       | EXISTS-AS-REDUCED-STAND-IN          | remark `InMemoryDurableOperationJournal.cs:17-30`: no grant consumption or audit, cannot record `Waiting`; zero production callers |
| `IDurableLeaseManager`, `IExecutionLease`                                                                                                                                                                                                                                                                                        | EXISTS, zero production callers     | `InMemoryDurableLeaseManager.cs:14`, `InMemoryExecutionLease.cs:8`                                                                 |
| `RecoveryEvidence`                                                                                                                                                                                                                                                                                                               | lacks `RecordedResult`, `NotBefore` | `RecoveryEvidence.cs:75-129`                                                                                                       |
| `DurableOperationState.Waiting`                                                                                                                                                                                                                                                                                                  | unreachable via journal             | `DurableOperationState.cs:44`                                                                                                      |
| `IDurableOperationCodec<T>`, `JsonDurableOperationCodec<T>`                                                                                                                                                                                                                                                                      | EXISTS-UNWIRED                      | –                                                                                                                                  |
| identity keys                                                                                                                                                                                                                                                                                                                    | EXISTS                              | `Abstractions/Identity/`                                                                                                           |
| `.Sqlite`, `.Json` journals; fenced decorator; `AddAgentDurability`; `AddDurabilityProfile`                                                                                                                                                                                                                                      | MISSING                             | –                                                                                                                                  |
| InMemory registrations                                                                                                                                                                                                                                                                                                           | un-keyed singletons                 | `Durability.InMemory/ServiceExtensions.cs:18-55`                                                                                   |
| consumers                                                                                                                                                                                                                                                                                                                        | zero                                | no durability references in `AgentKit`, `Loop`, `IO`, `Context.Compaction`                                                         |
| observability names                                                                                                                                                                                                                                                                                                              | partial                             | `durable.lease.*`, `durable.journal.*` exist; no `durable.execute/recover`                                                         |
| journal conformance suite                                                                                                                                                                                                                                                                                                        | MISSING                             | –                                                                                                                                  |

Test doubles: `IDurableOperationJournal` none outside its own tests;
`IExecutionLease` one
(`Abstractions.Tests/Durability/DurabilityTestData.cs:145`); `RecoveryEvidence`
constructed in Abstractions and InMemory tests (additive params keep them
green). Architecture tests already whitelist `AgentKit.Durability` and forbid
`Durability → Session`.

## Hidden prerequisites

1. `HookDispatchContext` (WS2): ship C3/C8 without the parameter.
2. Keyed InMemory registrations (`DurableExecutionContext` resolves keys).
3. Grant store and audit dispatcher availability for journal ingress (WS3); the
   journal fails closed at construction without them.
4. A local `IDurableExecutionBackend` must exist for a fully local composition;
   ship it in `Durability.InMemory`.
5. `SecurityOperationKind` has no durability kind; use `StateMutation`/
   `StateRead`.
6. `IIdentifierGenerator<CheckpointId>` and `<WorkerId>` registrations.
7. Consumers (C12) sequence after WS1, WS4, WS7, WS10, WS11.

## Spec coverage

| Contract                                                                              | Spec                                                                      |
| ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `DurableExecutionContext`, binding, descriptor, checkpoint, `RecoveryEvidence`        | `durable-execution.md:67-122` (evidence lacks the two new fields; update) |
| backend catalog/selector, runtime selector/lease, codec                               | `:152-197`                                                                |
| backend, journal (shown unwrapped), lease, policy, coordinator, event sink/dispatcher | `:207-300`; `AuthorizedDurableRequest<T>` NO-SPEC                         |
| `RecordWaitingAsync`, `DurableOperationWaiting`                                       | NO-SPEC (concept prose `:105-106,165-169`)                                |
| coordinator ctor; options; DI                                                         | `:341-354,387-527`                                                        |
| `UnknownEffectRecoveryMode`, `DurableCheckpointMode` members                          | NO-SPEC                                                                   |
| `DurabilityUnavailable`, `RecoveryIncompatible`, `OperatorActionRequired`             | NO-SPEC (prose `:577-583`)                                                |
| recovery decision table                                                               | `:612-624`; concept `:98-107`                                             |

## Chunks

### WS12-C1: Evidence fields and `DurableOperationWaiting`

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `RecoveryEvidence` gains
  `DurableOperationResult? RecordedResult` and `DateTimeOffset? NotBefore` with
  invariants; new `Durability/DurableOperationWaiting.cs`; tests; update the doc
  block. Snapshot: Abstractions.

### WS12-C2: `AuthorizedDurableRequest<T>` and `RecordWaitingAsync`

- Depends on: C1. Risk: CONTRACT-BREAK (one production impl; InMemory tests
  rewritten). Size: M.
- Deliverables: `Durability/AuthorizedDurableRequest.cs` mirroring
  `AuthorizedSessionStoreRequest`; all four journal methods take it;
  `RecordWaitingAsync`; `LoadEvidenceAsync` takes an authorized address request;
  InMemory adapts signatures. Snapshots: Abstractions, Durability.InMemory.

### WS12-C3: Backend, runtime, coordinator, event contracts

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: the 22 missing types listed above, one file each, with
  validating constructors and tests. Snapshot: Abstractions.

### WS12-C4: Journal and lease-manager conformance suites

- Depends on: C2. Risk: ADDITIVE. Size: M.
- Deliverables: `IDurableOperationJournalConformanceFixture`,
  `DurableOperationJournalConformanceTests`,
  `IDurableLeaseManagerConformanceFixture`,
  `DurableLeaseManagerConformanceTests`; InMemory fixtures.

### WS12-C5: InMemory keyed, grant-consuming, audited

- Depends on: C2, C4. Risk: DENSE-MODIFY `InMemoryDurableOperationJournal.cs`
  (410 lines), `ServiceExtensions.cs:18-55`. Size: M.
- Deliverables: `ISecurityGrantStore.ValidateAndConsumeAsync` before each write,
  audit dispatch, `RecordWaitingAsync`, `DurableJournalEnforcementReceipt`;
  keyed `AddInMemoryDurableOperationJournal(DurableJournalKey)`,
  `AddInMemoryDurableLeaseManager(DurableLeaseManagerKey)`; remove the stand-in
  remark. Snapshot: Durability.InMemory.

### WS12-C6: `AgentKit.Durability` runtime package

- Depends on: C3. Risk: ADDITIVE new project. Size: L.
- Deliverables: `AgentDurabilityOptions`, `DurabilityProfileOptions`,
  `DurabilityProfileSnapshot`, `DurabilityServiceRegistration`, every method in
  `durable-execution.md:401-526`, `DurableBackendCatalog`,
  `DurableBackendSelector`, `DurabilityRuntimeSelector`,
  `DurabilityRuntimeLease`, `DurableExecutionEventDispatcher`, generators,
  log/metrics; activity names `durable.execute/recover/dispatch/reconcile`;
  `InMemoryDurableExecutionBackend` in the InMemory leaf; tests project,
  snapshot, `AgentKit.slnx`.

### WS12-C7: `DefaultRecoveryPolicy` and fenced journal decorator

- Depends on: C6. Risk: ADDITIVE. Size: M.
- Deliverables: decision matrix over certainty × idempotency × unknown-effect
  mode; `FencedDurableOperationJournal` enforcing the lease token on every write
  with `LeaseLost`; matrix theory tests; concurrent-worker tests.

### WS12-C8: `DurableExecutionCoordinator`

- Depends on: C5, C6, C7. Risk: ADDITIVE. Size: L.
- Deliverables: `ExecuteAsync` (runtime lease → execution lease → start →
  dispatch → checkpoints → terminal) and `RecoverAsync` (evidence → policy →
  start/reconcile/retry/commit/operator); coordinator-owned renewal loop on
  `TimeProvider`; process-loss replay tests, lease loss mid-operation,
  cancellation at every await, activities and log IDs.

### WS12-C9: `AgentKit.Durability.Sqlite`

- Depends on: C4, C5. Risk: ADDITIVE new project. Size: L.
- Deliverables: journal and lease manager (token via `UPDATE … RETURNING`),
  database/schema/options/settings/target, keyed registrations,
  `JsonDurableOperationCodec<T>`; both suites plus reopen persistence.
  Descriptor claims host-local multi-process only.

### WS12-C10: `AgentKit.Durability.Json`

- Depends on: C4, C5. Risk: ADDITIVE new project. Size: M.
- Deliverables: journal over `JsonRecordLog` with torn-tail recovery and
  single-writer lock; no Json lease manager (documented). Suite plus torn append
  and second-writer tests.

### WS12-C11: Definition key and validator

- Depends on: C6. Risk: DENSE-MODIFY `AgentDefinition.cs` equality,
  `AgentCompositionValidator.cs`. Size: M.
- Deliverables: `DurabilityProfileKey? DurabilityProfile` (or on
  `OptionalCapabilities`); when set require coordinator, runtime selector,
  backend catalog, keyed journal/lease/policy/backend, codecs, generators;
  diagnostics `agentkit.durability.*`; one failing test each.

### WS12-C12a: Loop checkpoints

- Depends on: C8, C11, WS4, WS7. Risk: DENSE-MODIFY `DefaultAgentLoop` model
  request and tool dispatch sites. Size: L.
- Deliverables: the durable sandwich around provider attempts and tool
  invocations when a profile is selected; codecs for model-request and tool-call
  durable states; crash-after-outcome-ready → no reinvoke; unknown effect →
  operator.

### WS12-C12b: Engine, settlement, approval, compaction checkpoints

- Depends on: C12a, WS1, WS3, WS10. Risk: DENSE-MODIFY `AgentEngine` admission,
  IO promotion, compaction activation. Size: L.
- Deliverables: `RecordWaitingAsync` for approval waits; checkpoints after
  promotion, settlement, activation; process-loss replay through the engine.

### WS12-C13: Simple `WithDurability` and documentation

- Depends on: C6–C11. Size: S.
- Deliverables: Simple sugar and references; architecture and concept updates;
  durable-execution skill; READMEs.

## Totals

S 2, M 7, L 5. Confidence high on state; medium on C12 sizing.
