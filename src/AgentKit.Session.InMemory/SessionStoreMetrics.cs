// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>Owns the bounded concrete session-store outcome counter.</summary>
internal static class SessionStoreMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SessionStoreOperationCount,
        unit: "{operation}",
        description: "Number of terminal concrete session-store outcomes.");

    /// <summary>Records one store outcome with bounded operation and result dimensions.</summary>
    /// <param name="operation">The bounded store operation name.</param>
    /// <param name="outcome">The normalized terminal result kind.</param>
    internal static void Record(string operation, string outcome) =>
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
