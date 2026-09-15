// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>Owns the bounded authoritative session-directory outcome counter.</summary>
internal static class SessionDirectoryMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SessionDirectoryOperationCount, unit: "{operation}",
        description: "Number of terminal authoritative session-directory outcomes.");

    /// <summary>Records one bounded directory outcome.</summary>
    /// <param name="operation">The bounded directory operation.</param><param name="outcome">The bounded result kind.</param>
    internal static void Record(string operation, string outcome) => _operations.Add(1,
        new KeyValuePair<string, object?>(AgentKitTagNames.SessionOperation, operation),
        new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
