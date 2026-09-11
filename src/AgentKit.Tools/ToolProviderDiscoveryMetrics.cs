// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded discovery instruments, separate from capture lifetime and tool invocation.</summary>
/// <remarks>Callers contain instrument initialization, recording, and observer failures.</remarks>
internal static class ToolProviderDiscoveryMetrics
{
    /// <summary>Gets the shared discovery counter.</summary>
    /// <value>Completed operations tagged only with bounded outcomes.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolProviderDiscoveryCount, "{operation}", "Completed tool source discoveries.");
    /// <summary>Gets the shared discovery duration histogram.</summary>
    /// <value>Elapsed seconds; missing or reversed-clock measurements are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.ToolProviderDiscoveryDuration, "s", "Elapsed tool source discovery time.");
}
