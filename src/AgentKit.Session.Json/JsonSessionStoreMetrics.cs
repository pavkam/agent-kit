// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json;

/// <summary>Owns the bounded terminal-outcome counter emitted by the durable JSON session-store adapter.</summary>
/// <remarks>
/// The counter reuses the shared session-store metric name so dashboards aggregate across every store adapter. Only the
/// bounded operation name and the fixed outcome vocabulary are used as dimensions; no session, agent, or tenant identity
/// enters a metric.
/// </remarks>
internal static class JsonSessionStoreMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SessionStoreOperationCount,
        unit: "{operation}",
        description: "Number of terminal durable JSON session-store outcomes.");

    /// <summary>Records one store outcome with bounded operation and result dimensions.</summary>
    /// <param name="operation">The bounded store operation name.</param>
    /// <param name="outcome">The bounded terminal success, rejection, cancellation, or failure classification.</param>
    internal static void Record(string operation, string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation name is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }
}
