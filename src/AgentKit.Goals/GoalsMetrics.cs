// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Records bounded goal-runtime measurements without payload or identity dimensions.</summary>
internal static class GoalsMetrics
{
    private static readonly Counter<long> _taskDelegationPublications = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.TaskDelegationPublicationCount, unit: "{delegation}",
        description: "Number of terminal task-delegation dispatch outcomes.");
    private static readonly Histogram<double> _taskDelegationPublicationDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.TaskDelegationPublicationDuration, "s",
        "Duration of bounded task-delegation dispatch attempts.");

    /// <summary>Records one terminal task-delegation dispatch outcome using bounded dimensions only.</summary>
    /// <param name="outcome">The defined terminal dispatch outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordTaskDelegationPublication(TaskDelegationPublicationOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }

        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _taskDelegationPublications.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _taskDelegationPublicationDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }
}
