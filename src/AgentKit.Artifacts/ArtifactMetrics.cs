// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Records bounded artifact coordinator counters, durations, and sink-failure counts.</summary>
internal static class ArtifactMetrics
{
    private static readonly Counter<long> _operationCounter = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ArtifactOperationCount, unit: "{operation}", description: "Number of terminal artifact coordinator outcomes.");

    private static readonly Histogram<double> _operationDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ArtifactOperationDuration, "s", "Duration of artifact coordinator operations.");

    private static readonly Counter<long> _sinkFailureCounter = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ArtifactEventSinkFailureCount, unit: "{failure}", description: "Number of isolated artifact event sink delivery failures.");

    /// <summary>Records one terminal artifact outcome with only bounded dimensions.</summary>
    /// <param name="operation">The bounded operation label.</param>
    /// <param name="outcome">The bounded outcome label.</param>
    /// <param name="elapsed">The elapsed time, or <see langword="null"/> when unavailable.</param>
    internal static void Record(string operation, string outcome, TimeSpan? elapsed)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.ArtifactOperation, operation);
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _operationCounter.Add(1, tags);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _operationDuration.Record(duration.TotalSeconds, tags);
        }
    }

    /// <summary>Records one isolated sink delivery failure.</summary>
    /// <param name="outcome">The bounded failure class, either <c>failed</c> or <c>unavailable</c>.</param>
    internal static void RecordSinkFailure(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded failure class is required.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _sinkFailureCounter.Add(1, tags);
    }
}
