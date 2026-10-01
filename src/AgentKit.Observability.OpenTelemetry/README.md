# AgentKit.Observability.OpenTelemetry

OpenTelemetry bridge for AgentKit run events and security audit records. It
translates the neutral `RunEvent` and `SecurityAuditRecord` contracts into
`System.Diagnostics` activities, `Meter` counters, structured logs, and a
host-provided audit exporter. The package depends on the shared
`AgentKit.Observability` names and on no OpenTelemetry SDK type: hosts subscribe
to the `AgentKit` activity source and meter and own their providers and
exporters.

```csharp
services.AddOpenTelemetryObservability(
    new ObservationExporterKey("otel"),
    options =>
    {
        options.Signals = OpenTelemetrySignalSet.Activities | OpenTelemetrySignalSet.Metrics;
    });
```

## Behavior

- Registration is additive and keyed. Each key validates its options, captures
  an immutable snapshot, and registers a run-event sink (activities, metrics,
  logs) and, when the `Audit` signal is enabled, a security-audit sink.
  Repeating an identical registration is idempotent; reusing a key with
  different options throws.
- Spans, metrics, and logs carry only structural facts. Metrics use the bounded
  exporter key, event kind, and outcome as their only dimensions.
- Content capture is off by default. Enabling it needs an explicit
  `IObservationRedactor` (the omission-only default is rejected), a content
  classification the policy allows, and a byte bound. Content reaches a span
  only after a policy-compliant `RedactedContent` result; a redactor exception,
  an out-of-policy result, or a disallowed classification omits the content
  field and keeps the structural event.
- A best-effort run-event sink contains export failure. Setting
  `Delivery.Required` registers a required sink whose failure the publisher
  records as recovery-required settlement; the flush deadline bounds shutdown.
- The audit sink rethrows exporter failure so the permissions dispatcher decides
  isolation. Required audit delivery needs
  `AuditExporterProvidesDurableAcceptance`, and the host must register an
  `IOpenTelemetryAuditExporter`; there is no hidden default exporter.
