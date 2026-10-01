// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Defines content-free structured events for the OpenTelemetry observation exporter.</summary>
/// <remarks>This package owns event IDs 35000 through 35099. Captured content never appears in any event.</remarks>
internal static partial class OpenTelemetryObservationLog
{
    /// <summary>Records one run event delivered to the exporter.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="runId">The run that owns the event.</param>
    /// <param name="sequence">The event's per-run sequence.</param>
    /// <param name="eventKind">The bounded event kind.</param>
    [LoggerMessage(35000, LogLevel.Debug, "Observation exporter {ExporterKey} exported run {RunId} event {Sequence} of kind {EventKind}.")]
    internal static partial void RunEventExported(ILogger logger, string exporterKey, RunId runId, long sequence, string eventKind);

    /// <summary>Records one security audit record delivered to the audit exporter.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="recordId">The audit record identity.</param>
    /// <param name="eventKind">The bounded audit event kind.</param>
    [LoggerMessage(35001, LogLevel.Debug, "Observation exporter {ExporterKey} exported security audit record {RecordId} of kind {EventKind}.")]
    internal static partial void AuditRecordExported(ILogger logger, string exporterKey, SecurityAuditRecordId recordId, string eventKind);

    /// <summary>Records that captured content was omitted, naming only the bounded reason.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="contentKind">The bounded content kind.</param>
    /// <param name="reason">The bounded omission reason.</param>
    [LoggerMessage(35002, LogLevel.Debug, "Observation exporter {ExporterKey} omitted {ContentKind} content: {Reason}.")]
    internal static partial void ContentOmitted(ILogger logger, string exporterKey, string contentKind, string reason);

    /// <summary>Records an unexpected run-event export failure without payload content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="errorType">The exception type raised while exporting.</param>
    [LoggerMessage(35003, LogLevel.Warning, "Observation exporter {ExporterKey} failed to export a run event with error type {ErrorType}.")]
    internal static partial void RunEventExportFailed(ILogger logger, string exporterKey, string errorType);

    /// <summary>Records an audit export failure without record content.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="recordId">The audit record identity.</param>
    /// <param name="errorType">The exception type raised by the audit exporter.</param>
    [LoggerMessage(35004, LogLevel.Warning, "Observation exporter {ExporterKey} failed to export security audit record {RecordId} with error type {ErrorType}.")]
    internal static partial void AuditExportFailed(ILogger logger, string exporterKey, SecurityAuditRecordId recordId, string errorType);
}
