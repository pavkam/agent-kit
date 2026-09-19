// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded catalog coordination instruments shared by every terminal outcome.</summary>
/// <remarks>Callers isolate instrument initialization and recording failures from the coordination result.</remarks>
internal static class ToolCatalogCoordinationMetrics
{
    /// <summary>Gets the completed-operation counter.</summary>
    /// <value>Measurements carry only the bounded terminal outcome dimension.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolCatalogCoordinationCount, "{operation}", "Completed tool catalog coordination attempts.");
    /// <summary>Gets the elapsed-time histogram.</summary>
    /// <value>Seconds for valid, nonnegative clock measurements; missing durations are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.ToolCatalogCoordinationDuration, "s", "Elapsed tool catalog coordination time.");
}
