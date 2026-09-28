// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Exports redacted security audit records through host-provided OpenTelemetry machinery.</summary>
public interface IOpenTelemetryAuditExporter
{
    /// <summary>Writes one audit record that already passed redaction policy.</summary>
    /// <param name="record">The immutable audit record.</param>
    /// <param name="cancellationToken">Cancels the export wait only.</param>
    /// <returns>Completion of the exporter's bounded write attempt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> is null.</exception>
    public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default);
}
