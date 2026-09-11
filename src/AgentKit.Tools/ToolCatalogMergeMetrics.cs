// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded catalog merge instruments shared by coordination and policy decisions.</summary>
/// <remarks>Callers isolate instrument initialization and recording failures from semantic outcomes.</remarks>
internal static class ToolCatalogMergeMetrics
{
    /// <summary>Gets the completed-operation counter.</summary>
    /// <value>Measurements carry only the two merge operation names and four terminal outcomes.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolCatalogMergeCount, "{operation}", "Completed tool catalog merge operations.");
    /// <summary>Gets the elapsed-time histogram.</summary>
    /// <value>Seconds for valid, nonnegative clock measurements; missing durations are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.ToolCatalogMergeDuration, "s", "Elapsed tool catalog merge time.");
}
