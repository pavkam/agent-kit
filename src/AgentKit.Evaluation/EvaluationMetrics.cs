// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Records bounded evaluation measurements without identity or content dimensions.</summary>
internal static class EvaluationMetrics
{
    private static readonly Counter<long> _runs = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.EvaluationRunCount, unit: "{run}", description: "Number of terminal evaluation run outcomes.");

    private static readonly Counter<long> _cases = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.EvaluationCaseCount, unit: "{case}", description: "Number of terminal evaluation case repetition dispositions.");

    private static readonly Histogram<double> _caseDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.EvaluationCaseDuration, "s", "Duration of evaluation case repetitions.");

    private static readonly Counter<long> _evaluators = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.EvaluationEvaluatorCount, unit: "{evaluation}", description: "Number of terminal evaluator invocation outcomes.");

    private static readonly Counter<long> _exports = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.EvaluationExportCount, unit: "{export}", description: "Number of terminal report export outcomes.");

    /// <summary>Records one terminal run outcome.</summary>
    /// <param name="outcome">The bounded outcome name.</param>
    internal static void RecordRun(string outcome) =>
        _runs.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Records one terminal case repetition.</summary>
    /// <param name="disposition">The bounded disposition name.</param>
    /// <param name="elapsed">The measured duration, or <see langword="null"/> when the clock produced none.</param>
    internal static void RecordCase(string disposition, TimeSpan? elapsed)
    {
        var tag = new KeyValuePair<string, object?>(AgentKitTagNames.EvaluationCaseDisposition, disposition);
        _cases.Add(1, tag);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _caseDuration.Record(duration.TotalSeconds, tag);
        }
    }

    /// <summary>Records one evaluator invocation outcome.</summary>
    /// <param name="evaluatorKey">The configured evaluator key.</param>
    /// <param name="outcome">The bounded outcome name.</param>
    internal static void RecordEvaluator(string evaluatorKey, string outcome)
    {
        TagList tags = default;
        tags.Add(AgentKitTagNames.EvaluationEvaluatorKey, evaluatorKey);
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _evaluators.Add(1, tags);
    }

    /// <summary>Records one report export outcome.</summary>
    /// <param name="exporterKey">The configured exporter key.</param>
    /// <param name="outcome">The bounded outcome name.</param>
    internal static void RecordExport(string exporterKey, string outcome)
    {
        TagList tags = default;
        tags.Add(AgentKitTagNames.EvaluationExporterKey, exporterKey);
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _exports.Add(1, tags);
    }
}
