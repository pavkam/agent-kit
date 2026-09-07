// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded tool-invocation metric instruments.</summary>
internal static class ToolMetrics
{
    /// <summary>Gets the process-wide counter for terminal tool-call outcomes.</summary>
    internal static Counter<long> Calls { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ToolCallCount,
        unit: "{call}",
        description: "Number of terminal tool-call outcomes.");
}
