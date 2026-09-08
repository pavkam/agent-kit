// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Owns bounded measurements emitted by the SQLite security-grant adapter.</summary>
internal static class SqliteSecurityGrantStoreMetrics
{
    /// <summary>Gets the counter for terminal store-operation outcomes.</summary>
    /// <value>A process-wide counter carrying only bounded operation and outcome tags.</value>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityGrantStoreOperationCount,
        unit: "{operation}",
        description: "Number of terminal SQLite security-grant store operations.");

    /// <summary>Gets the histogram for safely measured store-operation duration.</summary>
    /// <value>A process-wide seconds histogram carrying only bounded operation and outcome tags.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.SecurityGrantStoreOperationDuration,
        unit: "s",
        description: "Duration of authoritative SQLite security-grant store operations.");
}
