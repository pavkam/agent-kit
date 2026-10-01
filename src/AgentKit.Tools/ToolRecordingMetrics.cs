// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Defines bounded recorder, retry, and event-delivery instruments on the shared AgentKit meter.</summary>
/// <remarks>Every dimension is a closed vocabulary: the record stage, the outcome, or the delivery result. Identities and content never become dimensions.</remarks>
internal static class ToolRecordingMetrics
{
    /// <summary>Counts completed recorder writes by stage and outcome.</summary>
    internal static readonly Counter<long> RecordCount = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ToolCallRecordCount,
        unit: "{record}",
        description: "Number of completed tool-call recorder writes.");

    /// <summary>Measures valid elapsed recorder-write seconds by stage and outcome.</summary>
    internal static readonly Histogram<double> RecordDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ToolCallRecordDuration,
        unit: "s",
        description: "Elapsed seconds of tool-call recorder writes.");

    /// <summary>Counts retry decisions by bounded outcome.</summary>
    internal static readonly Counter<long> RetryCount = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ToolRetryCount,
        unit: "{decision}",
        description: "Number of tool-invocation retry decisions.");

    /// <summary>Counts tool-event deliveries to sinks by bounded result.</summary>
    internal static readonly Counter<long> EventPublishCount = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ToolEventPublishCount,
        unit: "{delivery}",
        description: "Number of tool-event deliveries to registered sinks.");
}
