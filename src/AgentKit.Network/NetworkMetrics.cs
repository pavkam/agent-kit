// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Network;

/// <summary>Owns bounded network boundary metric instruments.</summary>
internal static class NetworkMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.NetworkOperationCount,
        unit: "{operation}",
        description: "Number of terminal network resolution and send outcomes.");

    /// <summary>Records one terminal network outcome without destinations or operation identities.</summary>
    /// <param name="stage">The bounded network stage name.</param>
    /// <param name="outcome">The normalized terminal result kind.</param>
    internal static void Record(string stage, string outcome) =>
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.NetworkStage, stage),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
