// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

using System.Collections.Concurrent;

/// <summary>An <see cref="IEvaluationReportExporter"/> that records every report and optionally scripts the answer.</summary>
internal sealed class RecordingReportExporter: IEvaluationReportExporter
{
    private readonly ConcurrentQueue<EvaluationReport> _reports = new();

    /// <summary>Gets or sets a factory that replaces the answer, or <see langword="null"/> to acknowledge the export.</summary>
    public Func<EvaluationReport, CancellationToken, ValueTask<EvaluationExportResult>>? OnExport { get; set; }

    /// <summary>Gets every report received, in call order.</summary>
    public IReadOnlyCollection<EvaluationReport> Reports => _reports;

    /// <inheritdoc/>
    public ValueTask<EvaluationExportResult> ExportAsync(EvaluationReport report, CancellationToken cancellationToken = default)
    {
        _reports.Enqueue(report);
        return OnExport is { } script
            ? script(report, cancellationToken)
            : ValueTask.FromResult<EvaluationExportResult>(new EvaluationExported("recording"));
    }
}
