// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json;

/// <summary>Owns bounded measurements emitted by the JSON approval-store adapter.</summary>
internal static class JsonApprovalStoreMetrics
{
    /// <summary>Gets the counter for terminal store-operation outcomes.</summary>
    /// <value>A process-wide counter carrying only bounded operation and outcome tags.</value>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityApprovalStoreOperationCount,
        unit: "{operation}",
        description: "Number of terminal JSON approval-store operations.");

    /// <summary>Gets the histogram for safely measured store-operation duration.</summary>
    /// <value>A process-wide seconds histogram carrying only bounded operation and outcome tags.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.SecurityApprovalStoreOperationDuration,
        unit: "s",
        description: "Duration of authoritative JSON approval-store operations.");
}
