// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Records bounded store measurements without identity or payload dimensions.</summary>
internal static class MemoryStoreMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.MemoryStoreOperationCount, unit: "{operation}", description: "Number of terminal memory, document, and vector store operation outcomes.");

    private static readonly Histogram<double> _duration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.MemoryStoreOperationDuration, "s", "Duration of memory, document, and vector store operations.");

    /// <summary>Records one terminal operation.</summary>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="family">The bounded state family.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="elapsed">The measured duration, or <see langword="null"/> when the clock produced none.</param>
    internal static void Record(string adapter, string family, string operation, string outcome, TimeSpan? elapsed)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(adapter), "Observation names a bounded adapter.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.MemoryStoreAdapter, adapter);
        tags.Add(AgentKitTagNames.MemoryStoreFamily, family);
        tags.Add(AgentKitTagNames.MemoryStoreOperation, operation);
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _operations.Add(1, tags);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _duration.Record(duration.TotalSeconds, tags);
        }
    }
}
