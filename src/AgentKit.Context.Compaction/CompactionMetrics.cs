// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Records bounded compaction outcomes without session or checkpoint identities.</summary>
internal static class CompactionMetrics
{
    private static readonly Counter<long> _attempts = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ContextCompactionCount);

    /// <summary>Increments the compaction attempt count with no checkpoint or session identities.</summary>
    /// <param name="outcome">The normalized terminal compaction outcome.</param>
    internal static void Record(string outcome) =>
        _attempts.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
