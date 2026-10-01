// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Publishes a finished evaluation report to one destination.</summary>
/// <remarks>
/// Export is a separate effect: it never shares a transaction with the result store, and its failure never changes a
/// recorded result or the returned report. External exporters use the file, network, and security contracts instead of direct
/// host access. Implementations are thread-safe and idempotent for one report identity where their destination allows.
/// </remarks>
public interface IEvaluationReportExporter
{
    /// <summary>Publishes one report.</summary>
    /// <param name="report">The finished report.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>The typed answer; a thrown exception is isolated by the runner and recorded as a faulted export.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ValueTask<EvaluationExportResult> ExportAsync(EvaluationReport report, CancellationToken cancellationToken = default);
}
