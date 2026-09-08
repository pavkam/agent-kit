// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Records bounded input/output runtime measurements without payload or identity dimensions.</summary>
internal static class IOMetrics
{
    private static readonly Counter<long> _promotionPlans = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.InputPromotionPlanCount);
    private static readonly Histogram<double> _promotionPlanDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.InputPromotionPlanDuration, "s");
    private static readonly Counter<long> _humanQuestionPublications = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.HumanQuestionPublicationCount, unit: "{publication}",
        description: "Number of terminal human-question publication outcomes.");
    private static readonly Histogram<double> _humanQuestionPublicationDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.HumanQuestionPublicationDuration, "s",
        "Duration of bounded human-question publication attempts.");

    /// <summary>Records one terminal planning outcome and duration using bounded dimensions only.</summary>
    /// <param name="boundary">The finite promotion boundary.</param><param name="outcome">The finite terminal planning outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="boundary"/> or <paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordPromotionPlan(PromotionBoundary boundary, InputPromotionPlanOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(boundary);
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }
        TagList tags = default;
        tags.Add(AgentKitTagNames.InputPromotionBoundary, boundary.ToString());
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _promotionPlans.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _promotionPlanDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }


    /// <summary>Records one terminal human-question publication outcome and duration using bounded dimensions only.</summary>
    /// <param name="outcome">The defined terminal publication outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordHumanQuestionPublication(HumanQuestionPublicationOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }

        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _humanQuestionPublications.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _humanQuestionPublicationDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }
}
