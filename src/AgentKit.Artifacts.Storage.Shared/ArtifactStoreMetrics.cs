// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

/// <summary>Records bounded artifact store counters and durations shared by every adapter.</summary>
internal static class ArtifactStoreMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ArtifactStoreOperationCount, unit: "{operation}", description: "Number of terminal artifact store operation outcomes.");

    private static readonly Histogram<double> _duration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ArtifactStoreOperationDuration, "s", "Duration of artifact store operations.");

    /// <summary>Records one terminal store outcome with only bounded dimensions.</summary>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="operation">The bounded operation label.</param>
    /// <param name="outcome">The bounded outcome label.</param>
    /// <param name="elapsed">The elapsed time, or <see langword="null"/> when unavailable.</param>
    internal static void Record(string adapter, string operation, string outcome, TimeSpan? elapsed)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(adapter), "Observation names a bounded adapter.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.ArtifactStoreAdapter, adapter);
        tags.Add(AgentKitTagNames.ArtifactStoreOperation, operation);
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _operations.Add(1, tags);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _duration.Record(duration.TotalSeconds, tags);
        }
    }
}
