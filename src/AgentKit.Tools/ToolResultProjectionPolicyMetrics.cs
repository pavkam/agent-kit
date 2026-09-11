// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded projection-policy lookup instruments independently from tool invocation metrics.</summary>
/// <remarks>The catalog isolates initialization and listener failures. Creating these instruments never initializes invocation counters.</remarks>
internal static class ToolResultProjectionPolicyMetrics
{
    /// <summary>Gets the process-wide counter of exact projection-policy lookups with only bounded outcome tags.</summary>
    /// <value>The shared AgentKit meter instrument; observations never contain policy keys, revisions, or content.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ToolResultProjectionPolicyResolutionCount,
        unit: "{resolution}",
        description: "Number of exact tool-result projection-policy resolutions.");

    /// <summary>Gets the process-wide histogram of measured projection-policy lookup duration.</summary>
    /// <value>Elapsed seconds with bounded outcome tags; failed clock measurements are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ToolResultProjectionPolicyResolutionDuration,
        unit: "s",
        description: "Elapsed time for exact tool-result projection-policy resolution.");
}
