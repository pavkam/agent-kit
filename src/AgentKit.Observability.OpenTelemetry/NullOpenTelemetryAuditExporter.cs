// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry;

/// <summary>Best-effort no-op audit exporter used when the host supplies no OpenTelemetry audit backend.</summary>
internal sealed class NullOpenTelemetryAuditExporter: IOpenTelemetryAuditExporter
{
    /// <inheritdoc/>
    public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
