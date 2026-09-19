# WS17: Observability completion

Goal: observation content and redaction contracts exist, every first-party
package is instrumented through the shared names, an
`AgentKit.Observability.OpenTelemetry` exporter leaf delivers run events and
security audit records, and required-sink failure participates in settlement.

Owning documents: [Observability](../architecture/observability.md),
[Observability and audit](../concepts/observability-and-audit.md).

## Progress

- [ ] WS17-C1 shared metric/log assertion helpers
- [ ] WS17-C2 observation content and redaction contracts
- [ ] WS17-C3 instrument `AgentKit.Artifacts` and `.InMemory`
- [ ] WS17-C4 instrument `AgentKit.Mcp` and `.Server`
- [ ] WS17-C5a instrument file tools
- [ ] WS17-C5b instrument the remaining tools
- [ ] WS17-C6 instrument `Budgets.Storage.Shared` and `Simple`
- [ ] WS17-C7 `AgentKit.Observability.OpenTelemetry`
- [ ] WS17-C8 required-sink settlement and shutdown flush

## Verified current state

| Item                                                                                                                                                                                                                                                                                                                        | State           | Evidence                                                                                                                                                  |
| --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ObservationExporterKey`, `ObservationExporterVersion`, `ObservationContent`, `ObservationContentKind`, `ContentFingerprint`, `IObservationRedactor`, `ObservationPolicy`, `RedactionResult`, capture/delivery policies, `ObservationBounds`, `DataClassification`, `OpenTelemetrySignalSet`, `IOpenTelemetryAuditExporter` | MISSING (14)    | only `ContentHash`, `NetworkDataClassification`, `ArtifactDataClassification` exist                                                                       |
| `IRunEventSink`, `RunEventSinkRegistration`                                                                                                                                                                                                                                                                                 | MISSING         | owned by WS1-C4                                                                                                                                           |
| `ISecurityAuditSink`, `SecurityAuditRecord`, `SecurityAuditSinkRegistration`, `AddSecurityAuditSink(registration, instance)`                                                                                                                                                                                                | EXISTS-AND-USED | instance-based, not the generic `<TSink>` form the spec shows                                                                                             |
| `AgentKit.Observability.OpenTelemetry`                                                                                                                                                                                                                                                                                      | MISSING         | –                                                                                                                                                         |
| `AgentKit.Observability`                                                                                                                                                                                                                                                                                                    | EXISTS          | 99 activity names, ~90 metric names, tags, `AddAgentKitObservability()` (logging only)                                                                    |
| packages with no Observability reference and zero instrumentation                                                                                                                                                                                                                                                           | MISSING         | `Artifacts`, `Artifacts.InMemory`, `Budgets.Storage.Shared`, `Mcp`, `Mcp.Server`, `Simple`, all 17 `Tools.*`, all 16 provider leaves (WS7 owns providers) |
| shared signal assertions                                                                                                                                                                                                                                                                                                    | PARTIAL         | `ActivityCollector`, `RecordingLogger` exist; no shared `MeterListener` collector (8 projects roll their own)                                             |
| event-ID ownership test                                                                                                                                                                                                                                                                                                     | EXISTS          | `Architecture.Tests/LoggerMessageEventIdTests.cs`; free ranges include 3000s, 9000s, 15000s, 16000s, 19000s, 25000+                                       |
| required-sink settlement                                                                                                                                                                                                                                                                                                    | MISSING         | depends on WS1-C5/C7                                                                                                                                      |

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

### WS17-C3: Instrument Artifacts

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: Observability reference, `ArtifactLog` (25000–25099),
  `artifact.prepare/finalize/abort/read/delete` activities,
  `agentkit.artifact.operation.count`; tests via C1 helpers asserting no
  content.

### WS17-C4: Instrument Mcp and Mcp.Server

- Depends on: –. Risk: ADDITIVE. Size: M.
- Deliverables: 13100–13199 and 13200–13299 blocks, `mcp.server.request`,
  `mcp.server.tool.call`, `agentkit.mcp.server.operation.count`; tests.

### WS17-C5a/C5b: Instrument the tool leaves

- Depends on: –. Risk: ADDITIVE, broad (17 csproj, 17 `<Tool>Log.cs`). Size: M
  each. C5a Read/Write/Edit/Patch/Glob/Search/List; C5b Command/Task/Plan/
  Question/Resource/Skill/Language/Web/WebSearch.
- Deliverables: `AgentKitActivityNames.ExecuteTool` reuse, `ToolId` tag if
  absent, one 100-ID block per package, one observability test per package
  asserting no raw path or argument content.

### WS17-C6: Instrument `Budgets.Storage.Shared` and `Simple`

- Depends on: –. Risk: ADDITIVE. Size: S.
- Deliverables: budget ledger log events (7100–7199); `simple.ask` activity and
  log block (27000+).

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

### WS17-C8: Required-sink settlement and shutdown flush

- Depends on: WS1-C5, WS1-C7, C7. Risk: DENSE-MODIFY
  `DefaultOutputPublisher.CompleteAsync` and the runtime settlement path (~40
  lines). Size: M.
- Deliverables: required sink failure → `RunSettlementRecoveryRequired`;
  `AgentEngine.DisposeAsync` drains required sinks within the delivery-policy
  deadline; tests for both; skill and architecture updates.

## Totals

S 2, M 6, L 1. Confidence medium: about nine named types are NO-SPEC, the
classification and fingerprint reuse decisions can ripple into other snapshots,
and C7 introduces the first OpenTelemetry dependency.
