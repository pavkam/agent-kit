// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

/// <summary>Records bounded output decisions without definitions, attempts, or content.</summary>
internal static class OutputMetrics
{
    private static readonly Counter<long> _decisions = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.OutputProcessingCount);

    /// <summary>Increments output decisions using only normalized outcome and mode dimensions.</summary>
    /// <param name="outcome">The normalized terminal processing decision.</param>
    /// <param name="mode">The bounded output protocol mode.</param>
    internal static void Record(string outcome, OutputMode mode) =>
        _decisions.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome),
            new KeyValuePair<string, object?>(AgentKitTagNames.OutputMode, mode.ToString()));
}
