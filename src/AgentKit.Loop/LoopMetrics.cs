// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop;

/// <summary>Owns bounded agent-loop metric instruments.</summary>
internal static class LoopMetrics
{
    /// <summary>Gets the process-wide counter for terminal run outcomes.</summary>
    internal static Counter<long> Runs { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.AgentRunCount,
        unit: "{run}",
        description: "Number of terminal agent-run outcomes.");

    /// <summary>Gets the process-wide histogram for settled run duration.</summary>
    internal static Histogram<double> RunDuration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.AgentRunDuration,
        unit: "s",
        description: "Elapsed duration of settled agent runs in seconds.");
}
