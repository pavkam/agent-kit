// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Owns bounded measurements emitted by the SQLite security-decision-store adapter.</summary>
internal static class SqliteSecurityDecisionStoreMetrics
{
    /// <summary>Gets the counter for terminal store-operation outcomes.</summary>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityDecisionStoreOperationCount,
        unit: "{operation}",
        description: "Number of terminal SQLite security-decision-store operations.");

    /// <summary>Gets the histogram for safely measured store-operation duration.</summary>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.SecurityDecisionStoreOperationDuration,
        unit: "s",
        description: "Duration of authoritative SQLite security-decision-store operations.");
}
