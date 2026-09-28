# AgentKit.Observability.OpenTelemetry

OpenTelemetry bridge for AgentKit run events and security audit records.
Register with
`AddOpenTelemetryObservability(ObservationExporterKey, Action<OpenTelemetryObservationOptions>)`.

Content capture is disabled by default. Exporter failures do not mutate semantic
run outcomes.
