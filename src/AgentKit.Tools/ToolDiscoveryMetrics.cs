// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns exporter-free bounded discovery/ownership instruments.</summary>
/// <remarks>Callers isolate initialization and listener failures; source identities never enter metric dimensions.</remarks>
internal static class ToolDiscoveryMetrics
{
    /// <summary>Gets the completed-operation counter.</summary>
    /// <value>Closed operation and outcome dimensions only.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolCatalogDiscoveryOperationCount, "{operation}", "Completed catalog discovery and ownership operations.");
    /// <summary>Gets elapsed-operation seconds.</summary>
    /// <value>Unknown or reversed-clock durations are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.ToolCatalogDiscoveryOperationDuration, "s", "Elapsed catalog discovery and ownership time.");
}
