// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.OpenTelemetry.Tests;

/// <summary>An audit exporter test double that records writes and can fail on demand.</summary>
internal sealed class RecordingAuditExporter: IOpenTelemetryAuditExporter
{
    private readonly List<SecurityAuditRecord> _records = [];

    /// <summary>Gets or sets the exception thrown by every write, when set.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets the records written so far.</summary>
    public IReadOnlyList<SecurityAuditRecord> Records => _records;

    /// <inheritdoc/>
    public ValueTask WriteAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Failure is { } failure)
        {
            throw failure;
        }

        _records.Add(record);
        return ValueTask.CompletedTask;
    }
}
