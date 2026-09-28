// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Delivers security audit records to a host-provided OpenTelemetry audit exporter.</summary>
internal sealed class OpenTelemetrySecurityAuditSink(
    IOpenTelemetryAuditExporter exporter,
    IObservationRedactor redactor,
    OpenTelemetryObservationOptionsSnapshot options): ISecurityAuditSink
{
    private readonly IOpenTelemetryAuditExporter _exporter = exporter;
    private readonly IObservationRedactor _redactor = redactor;
    private readonly OpenTelemetryObservationOptionsSnapshot _options = options;

    /// <inheritdoc/>
    public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken) =>
        OpenTelemetryObservationWriter.WriteAuditAsync(_exporter, _redactor, _options, record, cancellationToken);
}
