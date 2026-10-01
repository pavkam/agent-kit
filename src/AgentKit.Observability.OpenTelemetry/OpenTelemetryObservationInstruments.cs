// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Owns the bounded counters one exporter sink records on the shared AgentKit meter.</summary>
/// <remarks>Dimensions are limited to the exporter key, the bounded event or content kind, and the bounded outcome or reason.</remarks>
internal sealed class OpenTelemetryObservationInstruments
{
    private readonly Counter<long> _runEvents;
    private readonly Counter<long> _auditRecords;
    private readonly Counter<long> _contentOmitted;

    /// <summary>Creates the counters on <paramref name="meter"/>.</summary>
    /// <param name="meter">The meter that owns the instruments; AgentKit uses its shared meter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="meter"/> is null.</exception>
    internal OpenTelemetryObservationInstruments(Meter meter)
    {
        ArgumentNullException.ThrowIfNull(meter);
        _runEvents = meter.CreateCounter<long>(
            AgentKitMetricNames.ObservationRunEventCount, unit: "{event}", description: "Run events delivered to an observation sink.");
        _auditRecords = meter.CreateCounter<long>(
            AgentKitMetricNames.ObservationAuditRecordCount, unit: "{record}", description: "Security audit records delivered to an observation sink.");
        _contentOmitted = meter.CreateCounter<long>(
            AgentKitMetricNames.ObservationContentOmittedCount, unit: "{field}", description: "Captured content fields an observation sink omitted.");
    }

    /// <summary>Counts one run event.</summary>
    /// <param name="exporterKey">The exporter key.</param>
    /// <param name="eventKind">The bounded event kind.</param>
    /// <param name="outcome">The bounded outcome.</param>
    internal void RunEvent(string exporterKey, string eventKind, string outcome) =>
        _runEvents.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.ObservationExporterKey, exporterKey),
            new KeyValuePair<string, object?>(AgentKitTagNames.ObservationEventKind, eventKind),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Counts one audit record.</summary>
    /// <param name="exporterKey">The exporter key.</param>
    /// <param name="eventKind">The bounded audit event kind.</param>
    /// <param name="outcome">The bounded outcome.</param>
    internal void AuditRecord(string exporterKey, string eventKind, string outcome) =>
        _auditRecords.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.ObservationExporterKey, exporterKey),
            new KeyValuePair<string, object?>(AgentKitTagNames.SecurityAuditEventKind, eventKind),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Counts one omitted content field.</summary>
    /// <param name="exporterKey">The exporter key.</param>
    /// <param name="contentKind">The bounded content kind.</param>
    /// <param name="reason">The bounded omission reason.</param>
    internal void ContentOmitted(string exporterKey, string contentKind, string reason) =>
        _contentOmitted.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.ObservationExporterKey, exporterKey),
            new KeyValuePair<string, object?>(AgentKitTagNames.ObservationContentKind, contentKind),
            new KeyValuePair<string, object?>(AgentKitTagNames.ObservationContentOmittedReason, reason));
}
