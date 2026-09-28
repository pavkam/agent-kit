// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Records bounded artifact operation counters.</summary>
internal static class ArtifactMetrics
{
    private static readonly Counter<long> _operationCounter =
        AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ArtifactOperationCount);

    /// <summary>Records one terminal artifact outcome.</summary>
    /// <param name="operation">The bounded operation label.</param>
    /// <param name="outcome">The bounded outcome label.</param>
    internal static void Record(string operation, string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(operation), "A bounded operation is required.");
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded outcome is required.");
        try
        {
            _operationCounter.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome),
                new KeyValuePair<string, object?>(AgentKitTagNames.ArtifactOperation, operation));
        }
        catch
        {
        }
    }
}
