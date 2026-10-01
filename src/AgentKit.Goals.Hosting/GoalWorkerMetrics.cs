// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Hosting;

/// <summary>Records bounded worker measurements without identity or content dimensions.</summary>
internal static class GoalWorkerMetrics
{
    private static readonly Counter<long> _drains = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DelegationWorkerDrainCount, unit: "{intent}", description: "Number of delegation intents the worker drained, by bounded outcome.");

    private static readonly Counter<long> _messages = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.AgentMessageCount, unit: "{message}", description: "Number of agent-to-agent messages by bounded outcome.");

    /// <summary>Records one sent message.</summary>
    /// <param name="outcome">The bounded outcome.</param>
    internal static void RecordMessage(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A message records a bounded outcome.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _messages.Add(1, tags);
    }

    /// <summary>Records one drained intent.</summary>
    /// <param name="outcome">The bounded outcome.</param>
    internal static void RecordDrain(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A drain records a bounded outcome.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _drains.Add(1, tags);
    }
}
