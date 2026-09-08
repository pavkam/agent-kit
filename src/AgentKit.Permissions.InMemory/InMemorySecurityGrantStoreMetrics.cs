// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Owns bounded measurements emitted by the process-local security-grant store.</summary>
internal static class InMemorySecurityGrantStoreMetrics
{
    /// <summary>Gets the counter for terminal grant-consumption outcomes.</summary>
    /// <value>A process-wide counter carrying only the bounded outcome tag.</value>
    internal static Counter<long> GrantConsumptions { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityGrantConsumptionCount,
        unit: "{consumption}",
        description: "Number of terminal security-grant consumption outcomes.");
}
