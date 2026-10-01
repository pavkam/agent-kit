# WS17: Observability completion

Goal: observation content and redaction contracts exist, every first-party
package is instrumented through the shared names, an
`AgentKit.Observability.OpenTelemetry` exporter leaf delivers run events and
security audit records, and required-sink failure participates in settlement.

Owning documents: [Observability](../architecture/observability.md),
[Observability and audit](../concepts/observability-and-audit.md).

## Progress

- [x] WS17-C1 shared metric/log assertion helpers
- [x] WS17-C2 observation content and redaction contracts
- [x] WS17-C3 instrument `AgentKit.Artifacts` and `.InMemory`
- [x] WS17-C4 instrument `AgentKit.Mcp` and `.Server`
- [x] WS17-C5a instrument file tools
- [x] WS17-C5b instrument the remaining tools
- [x] WS17-C6 instrument `Budgets.Storage.Shared` and `Simple`
- [x] WS17-C7 `AgentKit.Observability.OpenTelemetry`
- [x] WS17-C8 required-sink settlement and shutdown flush

## Verified current state

| Item                                                                                                                                                                                                                                                                                                                               | State  | Evidence                                                                                                                                                                                                         |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ObservationExporterKey`, `ObservationExporterVersion`, `ObservationContent`, `ObservationContentKind`, `ContentFingerprint`, `IObservationRedactor`, `ObservationPolicy`, `RedactionResult`, capture/delivery policies, `ObservationBounds`, shared `DataClassification`; `OpenTelemetrySignalSet`, `IOpenTelemetryAuditExporter` | LANDED | `Abstractions/Observation/*`, `AgentKit.Observability.OpenTelemetry`; tests in `Abstractions.Tests/Observation`                                                                                                  |
| `IRunEventSink`, `RunEventSinkRegistration` (+ `FlushDeadline`), `IFlushableRunEventSink`, `RequiredRunEventSinkDrainResult`                                                                                                                                                                                                       | LANDED | `Abstractions/Results/*`; `AddRunEventSink<TSink>(registration, factory)` in `AgentKit.IO`                                                                                                                       |
| `ISecurityAuditSink`, `SecurityAuditSinkRegistration`, `AddSecurityAuditSink` instance, generic, and factory overloads                                                                                                                                                                                                             | LANDED | `Permissions/ServiceExtensions.cs`                                                                                                                                                                               |
| `AgentKit.Observability.OpenTelemetry`                                                                                                                                                                                                                                                                                             | LANDED | `src/AgentKit.Observability.OpenTelemetry`, `tests/AgentKit.Observability.OpenTelemetry.Tests` (64 tests), snapshot, slnx entry, `ProjectReferenceGraph` leaf test                                               |
| `AgentKit.Observability`                                                                                                                                                                                                                                                                                                           | LANDED | shared names plus observation activities, counters, tags, and `OmissionOnlyObservationRedactor` registered by `AddAgentKitObservability()`                                                                       |
| packages with no Observability reference and zero instrumentation                                                                                                                                                                                                                                                                  | LANDED | `Artifacts`, `Artifacts.InMemory`, `Budgets.Storage.Shared`, `Mcp.Server`, `Simple`, and all 16 `Tools.*` leaves are instrumented; `AgentKit.Mcp` is contracts-only with no runtime operation; providers are WS7 |
| shared signal assertions                                                                                                                                                                                                                                                                                                           | LANDED | `MetricCollector`, `MetricObservation`, `SignalAssertions` in `Test.Shared`, tested in `Observability.Tests`; adopted by Artifacts, Mcp.Server, the tool leaves, Simple, IO, Engine, and OpenTelemetry tests     |
| event-ID ownership test                                                                                                                                                                                                                                                                                                            | EXISTS | `Architecture.Tests/LoggerMessageEventIdTests.cs`; new blocks 13200 (Mcp.Server), 18200 (AgentKit), 22013-22015 (IO), 33000-34599 (tools), 35000 (OpenTelemetry); free: 3000s, 9000s, 15000s, 16000s, 36000+     |
| required-sink settlement                                                                                                                                                                                                                                                                                                           | LANDED | `DefaultOutputPublisher.SettlementOutcome`; `RequiredRunEventSinkCoordinator.DrainAsync`; `AgentEngineRuntime.DisposeWithDrainAsync`                                                                             |

## Hidden prerequisites

1. WS1-C4 and WS1-C5 must land before the OTel run-event sink.
2. `DataClassification`: shared enum in Abstractions (touches Network and
   Artifacts snapshots) versus an observation-local enum; the spec names
   `DataClassification`. WS14-C1 introduces the shared one.
3. `ContentFingerprint` versus reusing `ContentHash`.
4. The generic `AddSecurityAuditSink<TSink>(registration)` overload is needed in
   `AgentKit.Permissions` (additive, WS3 territory).
5. OTel packages need central version entries; check `make lint` policy.
6. Every newly instrumented package reserves a non-overlapping event-ID block
   and adds names to the shared classes (Observability snapshot).

## Spec coverage

| Contract                                                                                                     | Spec                               | Status  |
| ------------------------------------------------------------------------------------------------------------ | ---------------------------------- | ------- |
| exporter key/version, `ObservationContent`, `IRunEventSink`, `ISecurityAuditSink`, `IObservationRedactor`    | `observability.md:91-125`          | SPEC    |
| `ObservationContentKind`, `ContentFingerprint`, `ObservationPolicy`, `RedactionResult`, `DataClassification` | referenced, never defined          | NO-SPEC |
| capture/delivery policies, bounds, `OpenTelemetrySignalSet`, `IOpenTelemetryAuditExporter`, OTel options     | `observability.md:166-172,251`     | NO-SPEC |
| OTel sinks, options snapshot, `AddOpenTelemetryObservability(key, configure)`                                | `observability.md:163-258`         | SPEC    |
| sink registration semantics                                                                                  | `observability.md:141-143,264-267` | prose   |

## Chunks

### WS17-C1: Shared metric and log assertion helpers

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: `tests/AgentKit.Test.Shared/MetricCollector.cs`,
  `MetricObservation.cs`, `SignalAssertions.cs`
  (`ShouldNotContainContent(params string[])` over activities, logs, metrics);
  at least one existing project migrated.
- Landed: `MetricCollector`, `MetricObservation`, and
  `SignalAssertions.ShouldNotContainContent` already existed in `Test.Shared`
  from earlier workstreams, so this chunk added their missing tests
  (`MetricCollectorTests`, `SignalAssertionsTests`, `ActivityCollectorTests` in
  `AgentKit.Observability.Tests`, which now references `Test.Shared`). The
  helpers are adopted by Artifacts, Memory, Goals, Mcp.Server, every tool leaf,
  Simple, IO, Engine, and OpenTelemetry tests.

### WS17-C2: Observation content and redaction contracts

- Depends on: NO-SPEC blocks written first. Risk: ADDITIVE. Size: M.
- Deliverables: `Abstractions/Observation/ObservationExporterKey.cs`,
  `ObservationExporterVersion.cs`, `ObservationContentKind.cs`,
  `ContentFingerprint.cs`, `DataClassification.cs` (or reuse WS14's),
  `ObservationContent.cs`, `ObservationPolicy.cs`, `RedactionResult.cs`
  (+`RedactedContent`, `ContentOmitted`), `IObservationRedactor.cs`,
  `ObservationContentCapturePolicy.cs`, `ObservationDeliveryPolicy.cs`,
  `ObservationBounds.cs`; `Observability/OmissionOnlyObservationRedactor.cs`
  registered by `AddAgentKitObservability`. Snapshots: Abstractions,
  Observability.
- Landed: every listed Abstractions type and `OmissionOnlyObservationRedactor`
  (registered by `AddAgentKitObservability` via `TryAdd`) existed uncommitted
  from earlier work; this chunk tightened `ObservationContent` (a default
  payload is rejected) and added the missing tests
  (`Abstractions.Tests/Observation/*`, `OmissionOnlyObservationRedactorTests`,
  redactor registration tests). The NO-SPEC contracts and the shared
  `DataClassification` decision are recorded in `observability.md` ("Implemented
  shape and recorded deviations").

### WS17-C3: Instrument Artifacts

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: Observability reference, `ArtifactLog` (29000–29099; 25000
  belongs to `Session.Sqlite`), `artifact.prepare/finalize/abort/read/delete`
  activities, `agentkit.artifact.operation.count`; tests via C1 helpers
  asserting no content.
- Landed: `ArtifactLog` 29000-29005 (the block moved from the planned 25000,
  which belongs to `Session.Sqlite`), `artifact.*` activities, the
  `agentkit.artifact.operation.count/duration` instruments, and
  `ArtifactCoordinatorTests.Observability` (no content, replay key, owner, or
  media type in any signal) landed with WS15; verified, no further change.

### WS17-C4: Instrument Mcp and Mcp.Server

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: 13100–13199 and 13200–13299 blocks, `mcp.server.request`,
  `mcp.server.tool.call`, `agentkit.mcp.server.operation.count`; tests.
- Landed: `AgentKit.Mcp.Server` references `AgentKit.Observability`, adds
  `McpServerObservation` (`mcp.server.request`, `mcp.server.tool.call`,
  `agentkit.mcp.server.operation.count`, bounded tool name, content-free logs),
  `McpServerLog` 13200-13212, and a call-tool request filter installed by
  `AddAgentKitMcpServer` so SDK-dispatched reflected tools are observed too.
  `AgentKitPrimitiveHandler` gained a validating constructor. **Deviation:**
  `AgentKit.Mcp` is a contracts-only package with no runtime operation, so it
  gets no instrumentation and its 13100 block stays unclaimed. 12 new tests.

### WS17-C5a/C5b: Instrument the tool leaves

- Depends on: –. Risk: ADDITIVE, broad (17 csproj, 17 `<Tool>Log.cs`). Size: M
  each. C5a Read/Write/Edit/Patch/Glob/Search/List; C5b Command/Task/Plan/
  Question/Resource/Skill/Language/Web/WebSearch.
- Deliverables: `AgentKitActivityNames.ExecuteTool` reuse, `ToolId` tag if
  absent, one 100-ID block per package, one observability test per package
  asserting no raw path or argument content.
- Landed: all 16 tool leaves (Read, Write, Edit, Patch, Glob, Search, List;
  Command, Task, Plan and `todo`, Question, Resource, Skill, Language, Web,
  WebSearch) report through the redesigned `ToolLeafObservation.RunAsync`
  (shared `execute_tool` activity with `ToolId`,
  `agentkit.tool.leaf.operation.count`, cancellation and fault recording,
  rethrow unchanged, contained observer failure) and a package-owned `<Tool>Log`
  block (33000-34599) bound through `ToolLeafLogEvents`. Each tool constructor
  takes an `ILogger<TTool>` and `Add<Tool>Tool` registers
  `AddAgentKitObservability`. One observation test per package asserts the exact
  event ID, bounded metrics, and that a distinctive argument value is absent
  from every signal; shared wrapper tests are in `ToolLeafObservationTests` and
  `ToolLeafLogEventsTests`. The two listed chunks landed together because the
  change is mechanical and identical. **Breaking:** `ToolLeafObservation`
  changed from a disposable scope to a static `RunAsync`, and every tool
  constructor gained a logger parameter.

### WS17-C6: Instrument `Budgets.Storage.Shared` and `Simple`

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: budget ledger log events (7100–7199); `simple.ask` activity and
  log block (27000+).
- Landed: the budget-ledger log (7100) and metric were already shared by the
  three ledger adapters; `AgentKit.Simple` now starts `simple.ask` through
  `AgentKitActivityScope` with a typed outcome/error, contains observer failure,
  and keeps `SimpleLog` 27000-27001. New `SimpleAskObservabilityTests` and
  AskAsync signal tests assert no conversation text, reply, or provider secret
  leaks.

### WS17-C7: `AgentKit.Observability.OpenTelemetry`

- Depends on: C2, WS1-C4, WS1-C5, generic `AddSecurityAuditSink<TSink>`. Risk:
  ADDITIVE new project with new central package versions. Size: L.
- Deliverables: csproj, `GlobalUsings.cs`, `AssemblyInfo.cs`,
  `ServiceExtensions.cs`
  (`AddOpenTelemetryObservability(ObservationExporterKey, Action<OpenTelemetryObservationOptions>)`),
  options and snapshot, `OpenTelemetrySignalSet`, `OpenTelemetryRunEventSink`,
  `OpenTelemetrySecurityAuditSink`, `IOpenTelemetryAuditExporter`,
  `OpenTelemetryObservationWriter`, registration, README; tests project;
  snapshot; `ProjectReferenceGraph` leaf entry; `AGENTS.md` map and
  `project-structure.md`. Tests: exporter failure does not mutate outcome,
  duplicate key fails, content capture off by default, redaction failure omits.
- Landed: `AddOpenTelemetryObservability(key, configure)` validates options,
  captures one immutable per-key snapshot, and registers a run-event sink
  (activities, metrics, logs) and an audit sink through the new factory
  overloads of `AddRunEventSink`/`AddSecurityAuditSink`, with no static
  registry. Content capture is off by default, bounded, classified, and fails
  closed (redactor throw, out-of-policy result, disallowed classification omit
  the field, count it, and keep the structural event).
  `IOpenTelemetryAuditExporter` has no default; required audit delivery needs a
  durable exporter. 64 OTel tests cover duplicate/idempotent keys, validation,
  the capture-off default, redaction failure, exporter failure not changing the
  audit dispatch outcome, and the real permissions dispatcher. **Deviations:**
  no OpenTelemetry SDK package is referenced (the leaf uses the shared
  `ActivitySource`/`Meter`/`ILogger`, hosts own providers), so no central
  package entry or lint policy change was needed;
  `AddSecurityAuditSink<TSink>(registration)` already existed, so a factory
  overload was added instead; the project, snapshot, slnx entry,
  `ProjectReferenceGraph` test, and `project-structure.md` rows already existed,
  and this chunk added the full implementation, tests, README, and the
  `AGENTS.md` map entry.

### WS17-C8: Required-sink settlement and shutdown flush

- Depends on: WS1-C5, WS1-C7, C7. Risk: DENSE-MODIFY
  `DefaultOutputPublisher.CompleteAsync` and the runtime settlement path (~40
  lines). Size: M.
- Deliverables: required sink failure → `RunSettlementRecoveryRequired`;
  `AgentEngine.DisposeAsync` drains required sinks within the delivery-policy
  deadline; tests for both; skill and architecture updates.
- Landed: a required run-event sink's failure sets the publisher
  `SettlementOutcome` to `RunSettlementRecoveryRequired` (also when the sink
  throws before returning a task); `RunEventSinkRegistration` gained
  `FlushDeadline`, buffering sinks implement `IFlushableRunEventSink`, and
  `IRequiredRunEventSinkCoordinator.DrainAsync(CancellationToken)` flushes every
  required sink concurrently within its own deadline on the injected
  `TimeProvider`, reporting a `RequiredRunEventSinkDrainResult` instead of the
  previous fixed 30-second sleep. `AgentEngine.DisposeAsync` drains before
  disposing the owned provider and logs the result (18200-18201). **Deviation:**
  no edit to `DefaultOutputPublisher.CompleteAsync` was needed, because the loop
  already copies the publisher's `SettlementOutcome` into the final result; only
  the sink-throws-synchronously path needed a fix. **Breaking:** `DrainAsync`
  lost its `deadline` parameter and returns a result. Tests: coordinator,
  registration, drain result, IO registration, and engine disposal
  ordering/logging; skill and architecture docs updated.

## Totals

S 2, M 6, L 1. Confidence medium: about nine named types are NO-SPEC, the
classification and fingerprint reuse decisions can ripple into other snapshots,
and C7 introduces the first OpenTelemetry dependency.
