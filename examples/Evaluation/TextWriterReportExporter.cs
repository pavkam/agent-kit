// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace EvaluationExample;

/// <summary>Publishes a finished evaluation report as a short human-readable summary on a text writer.</summary>
/// <remarks>The summary holds identities, verdicts, and counts only, never prompts or model output, so it is safe to print or capture.</remarks>
internal sealed class TextWriterReportExporter(TextWriter writer): IEvaluationReportExporter
{
    /// <inheritdoc/>
    public async ValueTask<EvaluationExportResult> ExportAsync(EvaluationReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await writer.WriteLineAsync($"Evaluation run {report.RunId} of plan {report.PlanId} v{report.PlanVersion}: {report.Status}").ConfigureAwait(false);
        foreach (var result in report.Results)
        {
            var outcomes = string.Join(", ", result.Evaluators.Select(static e => $"{e.Key}={e.Outcome.Name}"));
            await writer.WriteLineAsync($"  {result.CaseId} #{result.Repetition}: {result.Verdict} ({outcomes})").ConfigureAwait(false);
        }

        var summary = report.Summary;
        await writer.WriteLineAsync($"  passed {summary.Passed}, failed {summary.Failed}, inconclusive {summary.Inconclusive}, not evaluated {summary.NotEvaluated}").ConfigureAwait(false);
        return new EvaluationExported("text writer");
    }
}
