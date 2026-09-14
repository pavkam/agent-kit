// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations;

/// <summary>Owns bounded conversational-turn metric instruments.</summary>
internal static class ConversationMetrics
{
    /// <summary>Gets the counter for terminal conversational-turn outcomes.</summary>
    internal static Counter<long> Turns { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ConversationTurnCount,
        unit: "{turn}",
        description: "Number of terminal conversational-turn outcomes.");

    /// <summary>Gets the histogram for conversational-turn duration in seconds.</summary>
    internal static Histogram<double> TurnDuration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ConversationTurnDuration,
        unit: "s",
        description: "Duration of one conversational turn.");

    /// <summary>Records one bounded terminal turn outcome and its measured duration.</summary>
    /// <param name="outcome">The stable, bounded outcome tag.</param>
    /// <param name="elapsed">A nonnegative measured duration.</param>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="elapsed"/> is negative.</exception>
    internal static void RecordTurn(string outcome, TimeSpan elapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome);
        Turns.Add(1, tags);
        TurnDuration.Record(elapsed.TotalSeconds, tags);
    }
}
