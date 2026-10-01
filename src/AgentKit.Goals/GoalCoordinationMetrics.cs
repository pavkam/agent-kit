// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Records bounded goal and delegation measurements without identity or content dimensions.</summary>
internal static class GoalCoordinationMetrics
{
    private static readonly Counter<long> _transitions = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.GoalTransitionCount, unit: "{transition}", description: "Number of committed goal status transitions.");
    private static readonly Counter<long> _delegations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DelegationCount, unit: "{delegation}", description: "Number of terminal delegation outcomes.");
    private static readonly Histogram<double> _delegationDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.DelegationDuration, "s", "Duration of delegations from authorization through the settlement wait.");
    private static readonly Counter<long> _joins = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.GoalJoinDecisionCount, unit: "{decision}", description: "Number of join decisions by strategy and kind.");

    /// <summary>Records one committed transition.</summary>
    /// <param name="from">The prior status.</param>
    /// <param name="to">The new status.</param>
    internal static void RecordTransition(GoalStatus from, GoalStatus to)
    {
        TagList tags = default;
        tags.Add(AgentKitTagNames.GoalFromStatus, from.ToString());
        tags.Add(AgentKitTagNames.GoalStatus, to.ToString());
        _transitions.Add(1, tags);
    }

    /// <summary>Records one terminal delegation outcome.</summary>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="elapsed">The measured duration, or null when the clock produced none.</param>
    internal static void RecordDelegation(string outcome, TimeSpan? elapsed)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A terminal delegation records a bounded outcome.");
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _delegations.Add(1, tags);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _delegationDuration.Record(duration.TotalSeconds, tags);
        }
    }

    /// <summary>Records one join decision.</summary>
    /// <param name="strategy">The strategy key.</param>
    /// <param name="decision">The bounded decision kind.</param>
    internal static void RecordJoin(string strategy, string decision)
    {
        TagList tags = default;
        tags.Add(AgentKitTagNames.GoalJoinStrategy, strategy);
        tags.Add(AgentKitTagNames.Outcome, decision);
        _joins.Add(1, tags);
    }
}
