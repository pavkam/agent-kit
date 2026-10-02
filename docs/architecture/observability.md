# Observability

**Role:** Explain behavior and prove security decisions without controlling the
runtime or leaking protected content.

[Observability includes distinct operational and audit signals](../concepts/observability-and-audit.md):
domain events, traces, metrics, structured logs, and a separate security audit
stream. These signals may share exporters, but their schemas, retention, access,
and delivery guarantees remain distinct.

Event and audit contracts live in AgentKit.Abstractions. Runtime components emit
through those contracts without depending on an exporter. A first-party
OpenTelemetry bridge belongs in AgentKit.Observability.OpenTelemetry; other
exporters remain independent leaf packages.

`AgentKit.Observability` owns the shared Microsoft diagnostics surface:
`AgentKitDiagnostics`, stable activity, metric, and tag names, and logging
registration. Runtime packages depend on this focused package and on
`Microsoft.Extensions.Logging.Abstractions`; they never depend on OpenTelemetry
or another exporter SDK. Hosts subscribe to its `ActivitySource`, `Meter`, and
ordinary `ILogger` categories. Neutral durable event and security-audit sinks
remain separate contracts because an activity or log record is not durable
semantic truth.

## Domain events

Durable semantic events are sufficient to reconstruct stable session and run
state. Live events describe provisional streaming, progress, and diagnostics.
Only the former are required for replay. Both carry stable domain correlation
instead of relying on formatted text.

Applicable signals correlate tenant, agent, session, conversation, run, turn,
request, message, input, tool call, deferred request, goal, and durable
operation. Provider request and MCP protocol identities are retained as external
correlation, not substituted for AgentKit identities.

## Telemetry

Traces describe causal work across context preparation, model requests, tool
batches, validation, persistence, and settlement. Metrics report bounded
aggregates such as latency, outcome, queue depth, retries, denial, token and
cost usage, context pressure, stream loss, compaction, and recovery delay. Logs
carry structured diagnostic facts and normalized error categories.

The shared source and meter name is `AgentKit`. Agent invocation uses the
OpenTelemetry GenAI `invoke_agent` operation, model inference uses its
applicable operation such as `chat`, and application tool execution uses
`execute_tool`. Stable AgentKit inner names cover turn preparation, context
assembly, session commit, output validation, and settlement. Public name
constants prevent packages from drifting independently.

[Domain errors have stable categories](../concepts/error-taxonomy.md), retry
advice, origin, safe message, external correlation, and side-effect certainty.
Provider or implementation details remain diagnostics; callers and models do not
parse raw exception text to decide behavior.

## Audit

Audit records authorization, permission rules, approvals, tool effects, memory
changes, configuration changes, MCP security context, recovery, branches,
reverts, and deletion. Records are append-oriented and redacted, with stronger
retention and access controls than routine telemetry.

Content capture is disabled by default. When enabled, prompts, output, tool
arguments, retrieval, and reasoning are separately classified, bounded,
policy-controlled, and redacted before export. Credentials and private keys are
never captured. Redaction failure removes the content field rather than the
structural event.

## Delivery and influence

Observers receive immutable data and cannot mutate run state. Best-effort sink
failure is isolated. A host may mark a durable event or audit sink as required;
failure then prevents clean settlement but remains bounded so shutdown cannot
deadlock.

Every interchangeable component is verified by a shared conformance suite.
Diagnostics expose behavior for those tests, but testability does not create
backdoors into private state.

AgentKit.Evaluation consumes public engine results, events, and recorded
manifests. It may export evaluation metrics through observability contracts, but
an evaluator is not an observer with secret access to mutable runtime state.

## Contract shape

The neutral contracts describe immutable delivery, not an exporter API. The
following is the minimum public shape; every named type belongs in its own file
and is documented for ordering, redaction, cancellation, and ownership.

```csharp
namespace AgentKit;

public readonly record struct ObservationExporterKey(string Value);

public readonly record struct ObservationExporterVersion(long Value);

public sealed record ObservationContent(
    ObservationContentKind Kind,
    DataClassification Classification,
    ImmutableArray<byte> Value,
    ContentFingerprint Fingerprint);

public interface IRunEventSink
{
    ValueTask PublishAsync(
        RunEvent runEvent,
        CancellationToken cancellationToken = default);
}

public interface ISecurityAuditSink
{
    ValueTask WriteAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken);
}

public interface IObservationRedactor
{
    ValueTask<RedactionResult> RedactAsync(
        ObservationContent content,
        ObservationPolicy policy,
        CancellationToken cancellationToken);
}
```

Supporting observation types referenced by sinks and redactors:

```csharp
public enum ObservationContentKind
{
    Prompt,
    ModelOutput,
    ToolArguments,
    ToolResult,
    RetrievedContent,
    Reasoning,
}

public enum DataClassification
{
    Public,
    Internal,
    Confidential,
    Restricted,
}

public readonly record struct ContentFingerprint(string Value);

public sealed record ObservationPolicy(
    ObservationBounds Bounds,
    ImmutableHashSet<DataClassification> AllowedClassifications);

public abstract record RedactionResult;

public sealed record RedactedContent(ObservationContent Content) : RedactionResult;

public sealed record ContentOmitted : RedactionResult;

public sealed record ObservationContentCapturePolicy(
    bool Enabled = false);

public sealed record ObservationDeliveryPolicy(
    bool Required = false,
    TimeSpan FlushDeadline = default);

public sealed record ObservationBounds(
    int MaximumBytesPerField = 0);
```

`SecurityAuditRecordId` is a dedicated validated readonly identifier. A run
event's stable protocol identity is the composite `(RunId, Sequence)`; the
sequence remains ordering within that run and is never used alone as a domain
ID. All other correlation uses the shared typed identities from
AgentKit.Abstractions. Records contain safe structural facts by default. Content
is represented only by an explicitly classified `ObservationContent` value whose
immutable byte array is owned by the record, and reaches a sink only after a
successful redaction result. A redaction failure produces an omitted-content
result, never the raw content as a fallback.

`SecurityAuditRecord` is the immutable shape owned by the security architecture;
observability does not redefine it. The package-owned fan-out and audit
dispatchers normalize cancellation, temporary or permanent delivery failure, and
exporter exceptions into the stable observer/audit error taxonomy. Signal
support is declared by `RunEventSinkRegistration` or
`SecurityAuditSinkRegistration` before delivery, so normal unsupported behavior
is never discovered by throwing from a sink.

## Implementations and service dependencies

No central runtime may depend on OpenTelemetry types. The leaf package
AgentKit.Observability.OpenTelemetry supplies `OpenTelemetryRunEventSink` and
`OpenTelemetrySecurityAuditSink`, translating the neutral records to activities,
metrics, logs, and the configured audit exporter. Custom JSON, database, SIEM,
or in-memory sinks implement the same contracts directly.

| Consumer or implementation                                    | Injected dependencies                                                                                                              |
| ------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| Loop, I/O, session, provider, tool, and durability components | Additive `IRunEventSink` registrations or their package-owned event fan-out, never exporter SDKs                                   |
| AgentKit.Permissions audit coordination                       | Additive `ISecurityAuditSink` registrations, effective audit-delivery policy, `TimeProvider`, and the security correlation context |
| OpenTelemetry sinks                                           | Package-owned validated options plus OpenTelemetry providers/exporters supplied by the host                                        |
| Content-capturing sink                                        | `IObservationRedactor`, size/classification policy, and an explicitly enabled capture option                                       |

The OpenTelemetry integration is an adapter, not a base class for custom
observers. Its representative dependency shapes are:

```csharp
namespace AgentKit.Observability.OpenTelemetry;

internal sealed record OpenTelemetryObservationOptionsSnapshot(
    ObservationExporterKey ExporterKey,
    ObservationExporterVersion ExporterVersion,
    OpenTelemetrySignalSet Signals,
    ObservationContentCapturePolicy ContentCapture,
    ObservationDeliveryPolicy Delivery,
    ObservationBounds Bounds);

internal sealed class OpenTelemetryRunEventSink(
    ActivitySource activities,
    Meter meter,
    IObservationRedactor redactor,
    OpenTelemetryObservationOptionsSnapshot options)
    : IRunEventSink
{
    public ValueTask PublishAsync(
        RunEvent runEvent,
        CancellationToken cancellationToken = default) =>
        OpenTelemetryObservationWriter.PublishRunEventAsync(
            activities,
            meter,
            redactor,
            options,
            runEvent,
            cancellationToken);
}

internal sealed class OpenTelemetrySecurityAuditSink(
    IOpenTelemetryAuditExporter exporter,
    IObservationRedactor redactor,
    OpenTelemetryObservationOptionsSnapshot options)
    : ISecurityAuditSink
{
    public ValueTask WriteAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken) =>
        OpenTelemetryObservationWriter.WriteAuditAsync(
            exporter,
            redactor,
            options,
            record,
            cancellationToken);
}
```

These named types live in separate matching files. Direct sink implementation
remains the extension path; inheritance is neither required nor useful here.

The component that owns a domain transition owns creation of its event. A sink
may neither mint domain events nor feed a decision back into the run. Required
audit delivery is enforced by the owning security or settlement component; the
sink itself does not acquire control authority.

## Lifetime, concurrency, and ownership

Stateless sinks and redactors are normally thread-safe singletons because one
built `AgentEngine` can run many agent definitions and many run scopes
concurrently. A required sink may instead be scoped when settlement awaits it;
its registration declares that lifetime and no singleton may capture it. Sinks
must not retain mutable run state. Immutable events may be delivered
concurrently, while ordering is guaranteed only for the correlation stream
declared by its registration. A sink that needs serialized writes owns its
bounded internal queue.

Best-effort sinks isolate failure and may drop only live, explicitly droppable
signals according to their registration. Required durable or audit records are
never silently dropped. Shutdown and flush accept cancellation and a configured
deadline; cancellation can stop waiting but cannot rewrite an accepted record as
persisted. The DI owner disposes exporters and sinks exactly once. A standalone
engine disposes its owned root provider; a hosted engine leaves that provider to
the host.

## Dependency-injection registration

Registration is additive and keyed by a stable sink name:

```csharp
namespace AgentKit.Observability.OpenTelemetry;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOpenTelemetryObservability(
            ObservationExporterKey key,
            Action<OpenTelemetryObservationOptions> configure) =>
            OpenTelemetryObservationRegistration.Add(
                services,
                key,
                configure);
    }
}
```

AgentKit.IO owns `AddRunEventSink<TSink>(RunEventSinkRegistration)` and
AgentKit.Permissions owns
`AddSecurityAuditSink<TSink>(SecurityAuditSinkRegistration)`; the OpenTelemetry
leaf calls those public registrations rather than creating a parallel sink
registry. Run-event and audit sinks are additive; registration order is the
deterministic fan-out order, while names provide stable selection and
diagnostics. Repeating an identical package registration is idempotent. Reusing
a name for a different implementation or delivery requirement fails validation.
There is no hidden default exporter and no unkeyed "last registration wins"
behavior. `IObservationRedactor` is singular and replaceable through ordinary
DI; the OpenTelemetry package always `TryAdd`s an omission-only redactor so the
sink graph remains valid without content capture. Enabling capture requires an
explicit replacement whose declared classifications and transformations satisfy
the configured policy; the omission-only default cannot accidentally become a
content exporter.

Each exporter key owns named `OpenTelemetryObservationOptions`. Registration
validates those mutable binding values and copies them into one immutable
`OpenTelemetryObservationOptionsSnapshot` for that keyed sink pair. Sinks never
inject unkeyed `IOptions<OpenTelemetryObservationOptions>`. These options are
captured, not monitored in place; changing them requires publishing a new
exporter version and provider generation, and existing deliveries finish with
their original snapshot. The registration helper creates the run-event and audit
keyed factories with the same `ObservationExporterKey` and closes each factory
over that exact snapshot. Ordinary constructor activation cannot pair a sink
with an unkeyed or differently keyed snapshot.

Routine `ILogger`, activity, and metric emission is best-effort observation.
Listener/exporter failure cannot alter accounting, authorization, semantic
outcomes, or commit decisions. Required semantic and audit sinks are different:
their delivery policy participates in the owning operation's commit/settlement
protocol. Their protected infrastructure uses the
[bounded bootstrap path](permissions-and-human-control.md#trusted-infrastructure-and-the-security-dependency-graph)
so grant persistence and audit export do not recurse through themselves.

## Build validation and unsupported behavior

Shared instrumentation is required; exporter and sink selection is optional.
Once a sink is registered, composition validates its unique name, declared
signal capabilities, lifetime, bounds, exporter dependencies, keyed immutable
options snapshot, and delivery policy. Enabling content capture additionally
requires an effective redactor, classification policy, and maximum sizes. A
required sink requires a bounded flush/settlement policy; an unavailable
required audit sink fails closed and prevents clean settlement.

An exporter that cannot represent a signal declares that capability before a
run. Unsupported content or signal kinds return a typed unsupported result and
are rejected or routed by explicit host policy; they do not disappear, throw a
surprise `NotSupportedException`, or fall back to an unredacted log. Domain IDs
are forbidden as unbounded metric labels even when an exporter technically
allows them.

## Implemented shape and recorded deviations

The sketches above are the minimum shape. The implementation differs in these
documented ways:

- **Contracts.** `ObservationContent`, `ObservationPolicy`, `ObservationBounds`,
  `ObservationDeliveryPolicy`, and `RedactedContent` are sealed immutable types
  with validating constructors rather than positional records, because each has
  caller-input invariants. A default `ImmutableArray<byte>` payload and a
  default `ContentFingerprint` are rejected. `ObservationBounds` requires a
  positive byte limit (no zero default);
  `ObservationDeliveryPolicy.FlushDeadline` defaults to a bounded 30 seconds and
  rejects a negative value; `ObservationExporterVersion` is positive.
  `DataClassification` is the shared Abstractions enum also used by artifacts,
  memory, and network contracts, and `ContentFingerprint` is a plain validated
  string (the `ContentHash` type stays a storage concern).
  `OmissionOnlyObservationRedactor` lives in `AgentKit.Observability` and is
  registered by `AddAgentKitObservability` through `TryAdd`.
- **Sink registration.** `AddRunEventSink<TSink>(registration, factory)` and
  `AddSecurityAuditSink<TSink>(registration, factory)` are additive overloads
  that build a singleton sink from the provider, so one sink type can be
  registered once per exporter key with its own immutable snapshot. The audit
  overload registers no unkeyed `TSink`. `RunEventSinkRegistration` gained
  `FlushDeadline`.
- **Required run-event sinks.** A required sink's inline failure becomes
  `RunSettlementRecoveryRequired` (publisher `SettlementOutcome`, copied by the
  loop). A buffering sink implements `IFlushableRunEventSink`; engine disposal
  calls `IRequiredRunEventSinkCoordinator.DrainAsync`, which flushes every
  required sink concurrently within that sink's own `FlushDeadline` and reports
  drained, timed-out, and faulted sinks instead of throwing. The previous fixed
  30-second wait was replaced.
- **OpenTelemetry leaf.** The package consumes no OpenTelemetry SDK package: it
  writes through the shared `ActivitySource`, `Meter`, and `ILogger` surfaces,
  so hosts attach their own providers and exporters and no central package
  version entry is needed. Run events become `observation.run_event.export`
  activities (never one span per delta stream), bounded counters, and
  35000-series log events; audit records become `observation.audit.export`
  activities, counters, and a call to the host's `IOpenTelemetryAuditExporter`.
  There is no default audit exporter: enabling the audit signal without one
  fails when the sink is resolved. The audit sink takes no
  `IObservationRedactor` because audit records are pre-redacted fingerprints; it
  rethrows exporter failure so the permissions dispatcher owns the
  required-versus-best-effort decision. Required audit delivery is rejected at
  registration unless the exporter declares durable acceptance. The internal
  writer takes an `ILogger` and an instruments object in addition to the
  sketched `ActivitySource`, `Meter`, and options parameters.
- **Registration semantics.** Each exporter key captures one immutable
  `OpenTelemetryObservationOptionsSnapshot`; repeating an identical registration
  is idempotent and reusing a key with different options throws. No static
  registry exists: the declaration lives in the service collection. Content
  capture is validated at registration (an allowed classification, the
  activities signal) and at sink activation (an explicit non-omission redactor).
- **Content capture.** Only `ContentDeltaEvent` carries content. The exporter
  declares its content classification, bounds the payload before redaction,
  fingerprints what it captured, and exports content as a span tag only when the
  redactor returned `RedactedContent` that is still within the allowed
  classifications, the byte bound, and the original content kind. A thrown
  redactor, an out-of-policy result, an unknown result variant, or a disallowed
  classification omits the field with a bounded reason, increments the omission
  counter, and keeps the structural event. Cancellation is the only exception
  that propagates.
- **Instrumented leaves.** The sixteen built-in tool packages report through
  `ToolLeafObservation.RunAsync` and a package-owned `ToolLeafLogEvents`
  (`execute_tool` activity, `agentkit.tool.leaf.operation.count`, and 100-ID
  blocks from 33000 to 34599; the `todo` alias reports under its own identity).
  `AgentKit.Mcp` contains only contracts and one-shot reflection validation with
  no runtime operation, so it emits nothing and its 13100 block stays unclaimed.
  Artifacts use 29000, `AgentKit.Simple` 27000, the shared budget-ledger log
  7100, engine shutdown 18200, and required-sink draining the IO 22013 events.
- **Tool runtime.** `AgentKit.Tools` reports recorder writes under
  `tool.call.record_accepted` and `tool.call.record_terminal` activities with
  the `agentkit.tool.call.record.count` and `.duration` metrics (record stage
  and outcome only) and log events 4110 and 4111; execution-policy selection
  logs 4120 to 4122; a failed or timed-out event sink logs 4130 and counts
  `agentkit.tool.event.publish.count`; a retry wait runs under
  `tool.retry.backoff` with log event 4140 and the `agentkit.tool.retry.count`
  decision counter. None carries arguments, results, aliases, or exception text.

## Related concept specifications

- [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)
- [Observability and audit](../concepts/observability-and-audit.md)
- [Error taxonomy](../concepts/error-taxonomy.md)
- [Testing and evaluation](../concepts/testing-and-evaluation.md)
- [Testing and evaluation architecture](testing-and-evaluation.md)
