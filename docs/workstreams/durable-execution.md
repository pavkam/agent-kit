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

- [x] WS12-C1 `RecoveryEvidence.RecordedResult`/`NotBefore`,
      `DurableOperationWaiting`
- [x] WS12-C2 `AuthorizedDurableRequest<T>` and `RecordWaitingAsync`
- [x] WS12-C3 backend, runtime, coordinator, event contracts
- [x] WS12-C4 journal and lease-manager conformance suites
- [x] WS12-C5 InMemory keyed, grant-consuming, audited
- [x] WS12-C6 `AgentKit.Durability` runtime package
- [x] WS12-C7 `DefaultRecoveryPolicy` and fenced journal decorator
- [x] WS12-C8 `DurableExecutionCoordinator`
- [x] WS12-C9 `AgentKit.Durability.Sqlite`
- [x] WS12-C10 `AgentKit.Durability.Json`
- [x] WS12-C11 definition key and validator
- [x] WS12-C12a loop checkpoints
- [x] WS12-C12b engine, settlement, approval, compaction checkpoints
- [x] WS12-C13 Simple `WithDurability` and documentation

## Verified current state

| Item                                                                                                             | State | Evidence                                                                                               |
| ---------------------------------------------------------------------------------------------------------------- | ----- | ------------------------------------------------------------------------------------------------------ |
| `AgentKit.Durability` runtime package                                                                            | DONE  | coordinator, fenced journal, recovery policy, profile registry, catalogs, selectors, event dispatcher  |
| backend, runtime, coordinator, dispatch, reconciliation, and event contracts                                     | DONE  | `Abstractions/Durability/`                                                                             |
| `DefaultRecoveryPolicy`, `FencedDurableOperationJournal`, `DurableExecutionCoordinator`, `ExecutionLeaseRenewal` | DONE  | `AgentKit.Durability/`; 187 tests in `AgentKit.Durability.Tests`                                       |
| `AddAgentDurability`, `AddDurabilityProfile`, keyed journal/lease/policy/backend registrations                   | DONE  | `DurabilityServiceRegistration.cs`; no persistence target is ever registered by default                |
| `InMemoryDurableOperationJournal` keyed, grant-consuming, audited; `RecordWaitingAsync`                          | DONE  | `Durability.InMemory/`; 181 tests                                                                      |
| `InMemoryDurableExecutionBackend`                                                                                | DONE  | claims neither external handoff nor reconciliation, and refuses both                                   |
| journal and lease-manager conformance suites                                                                     | DONE  | `AgentKit.Conformance/Durability/`                                                                     |
| definition key and composition validator                                                                         | DONE  | `AgentOptionalCapabilitySelection.DurabilityProfile`, `AgentKit/DurabilityCompositionValidator.cs`     |
| observability names and event IDs                                                                                | DONE  | `durable.execute/recover/dispatch/reconcile`; `AgentKit.Durability` owns event IDs 26000-26009         |
| `AgentKit.Durability.Sqlite` journal, lease manager, and backend                                                 | DONE  | `Durability.Sqlite/`; 137 tests including both conformance suites and reopen persistence               |
| `AgentKit.Durability.Json` journal                                                                               | DONE  | `Durability.Json/`; 88 tests including the journal suite, torn-append recovery, and second-writer      |
| loop model-attempt and tool-call checkpoints                                                                     | DONE  | `DefaultAgentLoop` via `DurableBoundaryScope`, two boundary handlers; profile-gated per operation name |
| coordinator-owned checkpoint writer on the handler contract                                                      | DONE  | `IDurableCheckpointWriter`, `DurableInvocationContext.Checkpoints`, `CoordinatorCheckpointWriter`      |
| shared boundary mechanics                                                                                        | DONE  | `DurableBoundaryScope`, `DurableBoundaryRegistry`, `DurableBoundaryHandler` in `Abstractions`          |
| engine admission checkpoint                                                                                      | DONE  | `AgentEngineRuntime.JournalRunAdmissionAsync`; `AgentKit.Tests` (442), `AgentKit.Simple.Tests`         |
| input promotion and run settlement checkpoints                                                                   | DONE  | `DefaultAgentLoop` sites, handlers in `AgentKit.IO`; `AgentKit.Loop.Tests` (272), `IO.Tests` (317)     |
| compaction activation checkpoint                                                                                 | DONE  | `SessionCompactionActivationCoordinator`; `AgentKit.Context.Compaction.Tests` (207)                    |
| approval wait via `RecordWaitingAsync`                                                                           | DONE  | `DurableApprovalWaitRecorder`, called by `DefaultToolExecutor`; `Permissions.Tests` (259)              |
| process-loss replay through a real engine                                                                        | DONE  | `AgentKit.Simple.Tests` over a journal decorator that loses a terminal commit                          |
| handler-per-enabled-operation composition check                                                                  | DONE  | `agentkit.durability.handler.missing` in `DurabilityCompositionValidator`                              |
| `AgentKit.Simple` `WithDurability`                                                                               | DONE  | `AgentEngineBuilderExtensions.WithDurability`; `AgentKit.Simple.Tests` (132)                           |

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
  database/schema/options/settings/target, keyed registrations; both suites plus
  reopen persistence. Descriptor claims host-local multi-process only.
- Landed: the journal and lease manager share one `SqliteDurableDatabase` the
  host supplies, so a lease generation actually fences a journal write. Portable
  evidence is encoded with `JsonStoreSerialization` into a BLOB plus a SHA256
  digest, which is why the SQLite and JSON leaves persist identical shapes
  instead of each owning a private binary codec.

### WS12-C10: `AgentKit.Durability.Json`

- Depends on: C4, C5. Risk: ADDITIVE new project. Size: M.
- Deliverables: journal over `JsonRecordLog` with torn-tail recovery and
  single-writer lock; no Json lease manager (documented). Suite plus torn append
  and second-writer tests.
- Landed: the log is appended before the in-memory projection is advanced, so a
  failed append can never leave live state ahead of the evidence. The adapter
  ships no lease manager because an advisory lock that rejects a second writer
  cannot honestly coordinate ownership between processes.

### WS12-C11: Definition key and validator

- Depends on: C6. Risk: DENSE-MODIFY `AgentDefinition.cs` equality,
  `AgentCompositionValidator.cs`. Size: M.
- Deliverables: `DurabilityProfileKey? DurabilityProfile` on
  `AgentOptionalCapabilities`; when set require coordinator, runtime selector,
  backend catalog, event dispatcher, keyed journal/lease/policy/backend, and the
  checkpoint/worker identifier generators; diagnostics `agentkit.durability.*`
  from `DurabilityCompositionValidator`; one failing test each.

### WS12-C12a: Loop checkpoints

- Depends on: C8, C11, WS4, WS7. Risk: DENSE-MODIFY `DefaultAgentLoop` model
  request and tool dispatch sites. Size: L.
- Deliverables: the durable sandwich around provider attempts and tool
  invocations when a profile is selected; codecs for model-request and tool-call
  durable states; crash-after-outcome-ready → no reinvoke; unknown effect →
  operator.
- Landed (the loop-private `LoopDurableScope` and
  `LoopDurableInvocationRegistry` named here were generalized into
  `DurableBoundaryScope` and `DurableBoundaryRegistry` in C12b):
  `LoopDurableScope` resolves the definition's profile once per run and journals
  only the boundaries that profile enables, so selecting durability never
  changes what the loop computes. Each boundary publishes its live continuation
  into `LoopDurableInvocationRegistry` for exactly one operation identity; a
  recovering process finds none and refuses rather than inventing a terminal
  record. Committing an already-recorded terminal result never reaches a
  handler, so crash-after-outcome-ready still cannot reinvoke, and both
  boundaries declare themselves non-idempotent so an unknown effect escalates to
  an operator. Payloads are identity-and-count manifests: prompts, arguments,
  and results never enter a durable record.

### WS12-C12b: Engine, settlement, approval, compaction checkpoints

- Depends on: C12a, WS1, WS3, WS10. Risk: CONTRACT-BREAK (handler contract) and
  DENSE-MODIFY `AgentEngine` admission, loop promotion and settlement sites,
  compaction activation. Size: L.
- Deliverables: `RecordWaitingAsync` for approval waits; checkpoints after
  promotion, settlement, activation; process-loss replay through the engine.
- Landed: the contract gap found in C12a is closed first. The coordinator hands
  every handler invocation a `DurableInvocationContext` whose
  `IDurableCheckpointWriter` is bound to that one attempt and fencing generation
  and revoked when the invocation returns, so a handler writes checkpoints and
  waiting records without holding the journal, the lease, the captured grant, or
  the checkpoint-id generator. The shared mechanics live in `Abstractions`
  (`DurableBoundaryScope`, `DurableBoundaryRegistry`, `DurableBoundaryHandler`)
  because a runtime package may reference only `Abstractions` and
  `Observability`; C12a's `LoopDurableScope` and its loop-private registry were
  replaced in place by them, and every first-party boundary uses the one
  mechanism. Seven boundaries now journal: run admission, input promotion, model
  request, tool call, compaction activation, approval wait, and run settlement.
  Each carries an identity-and-count manifest; none carries content.
- Landed, effect ordering: the model attempt, each tool call, and compaction
  activation call `DurableRecordResult.ThrowIfNotRecorded()` on their
  checkpoint, so a refused or fenced write stops the boundary before the
  provider, tool, or session append is reached. Settlement and promotion are
  confirming evidence of work already done, so a refusal is logged and never
  undoes it.
- Landed, deviation, approval wait: the wait is recorded by
  `IApprovalWaitRecorder` (`DurableApprovalWaitRecorder` in `Permissions`),
  called by the component that observes a `SecurityApprovalRequired` decision,
  not from inside `SecurityAuthority`. The first cut did record it in the
  authority and made `ISecurityAuthority` depend on
  `IDurableExecutionCoordinator`; the coordinator authorizes its own writes
  through the authority, so the two constructed each other and
  `AgentEngineBuilder.Build()` deadlocked the container. The composition rules
  exclude `Lazy<T>`/`Func<T>` as a fix, so the edge was removed rather than
  deferred. The tool executor is the first-party observer. Recorded in
  `docs/architecture/durable-execution.md`.
- Landed, defects found by the new end-to-end tests and fixed: the coordinator's
  checkpoint writer authorized checkpoints as `Mutate` while every journal
  enforces `Append`, so every handler checkpoint was refused at the journal and
  the boundaries discarded the result, leaving recovery with no checkpoint and a
  wrong `Start` decision; recovering an already-settled operation tried to
  re-stamp its committed terminal record and failed instead of returning the
  recorded result; and `WithDurability` enabled promotion, settlement, and
  activation with no registered handler, so settlement was never journaled. A
  composition check now requires a handler for every operation a selected
  profile enables. The coordinator tests use a journal fake that never enforces
  effects, which is why the first defect survived; the simple-composition tests
  run the real in-memory journal.
- Landed, unrelated failures fixed so the gates pass: engine disposal resolved a
  service from a provider already being disposed (129 test failures) and could
  deadlock on an asynchronous drain when the standalone provider disposed the
  engine singleton; a required run-event sink that threw synchronously escaped
  fault recording; event-ID collisions between `Artifacts`/`Session.Sqlite` and
  `Durability.Sqlite`/`Simple` (Artifacts moved to 29000, Durability.Sqlite to
  30000); budget-ledger tests still asserted pre-consolidation event IDs; a
  project-graph fixture named nodes it never defined; API snapshots were stale.

### WS12-C13: Simple `WithDurability` and documentation

- Depends on: C6–C11. Size: S.
- Deliverables: Simple sugar and references; architecture and concept updates;
  durable-execution skill; READMEs.
- Landed: `WithDurability` registers the process-local in-memory journal, lease
  manager, and backend, the default recovery policy, and one profile enabling
  all seven boundaries, selected on every hosted definition. It registers the
  boundary handlers whose owning package Simple does not otherwise compose
  (`AgentKit.IO`, compaction) with `TryAddEnumerable` so each registration is
  idempotent with the owner's, in either order relative to `WithCompaction`. It
  claims no crash recovery, because the adapters are explicitly ephemeral. The
  architecture document gains the handler and writer contracts, the
  seven-boundary table, the recorder deviation, the simple composition, the
  handler-per-operation validation rule, and settled-operation recovery; the
  concept specification gains the writer, effect-authorization, and settled
  recovery rules with acceptance scenarios; the durable-execution skill and the
  Abstractions, Durability, Loop, IO, Permissions, Compaction, facade, and
  Simple READMEs describe the same shapes.

## Totals

S 3, M 7, L 5, all landed. The C12b contract gap is closed, and the defects the
new end-to-end tests found are recorded in its Landed notes.
