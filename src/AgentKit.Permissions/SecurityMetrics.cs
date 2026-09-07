// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Owns bounded security-authorization metric instruments.</summary>
internal static class SecurityMetrics
{
    /// <summary>Gets the process-wide counter for terminal authorization decisions.</summary>
    internal static Counter<long> Decisions { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityDecisionCount,
        unit: "{decision}",
        description: "Number of terminal security authorization decisions.");
}
