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

## Build validation and unsupported behavior

Observability is optional. Once a sink is registered, composition validates its
unique name, declared signal capabilities, lifetime, bounds, exporter
dependencies, keyed immutable options snapshot, and delivery policy. Enabling
content capture additionally requires an effective redactor, classification
policy, and maximum sizes. A required sink requires a bounded flush/settlement
policy; an unavailable required audit sink fails closed and prevents clean
settlement.

An exporter that cannot represent a signal declares that capability before a
run. Unsupported content or signal kinds return a typed unsupported result and
are rejected or routed by explicit host policy; they do not disappear, throw a
surprise `NotSupportedException`, or fall back to an unredacted log. Domain IDs
are forbidden as unbounded metric labels even when an exporter technically
allows them.

## Related concept specifications

- [Streaming and event protocol](../concepts/streaming-and-event-protocol.md)
- [Observability and audit](../concepts/observability-and-audit.md)
- [Error taxonomy](../concepts/error-taxonomy.md)
- [Testing and evaluation](../concepts/testing-and-evaluation.md)
- [Testing and evaluation architecture](testing-and-evaluation.md)
