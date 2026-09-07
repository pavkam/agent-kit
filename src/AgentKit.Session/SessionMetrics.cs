// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Owns bounded session-coordination metric instruments.</summary>
internal static class SessionMetrics
{
    /// <summary>Gets the process-wide counter for terminal session operation outcomes.</summary>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SessionOperationCount,
        unit: "{operation}",
        description: "Number of terminal session coordination outcomes.");
}
