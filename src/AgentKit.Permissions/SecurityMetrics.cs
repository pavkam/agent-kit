// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Owns bounded security-authorization metric instruments.</summary>
internal static class SecurityMetrics
{
    /// <summary>Gets the process-wide counter for terminal authorization decisions.</summary>
    internal static Counter<long> Decisions { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityDecisionCount,
        unit: "{decision}",
        description: "Number of terminal security authorization decisions.");

    /// <summary>Gets the counter for terminal captured-authority selection outcomes.</summary>
    internal static Counter<long> AuthoritySelections { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityAuthoritySelectionCount,
        unit: "{selection}",
        description: "Number of terminal captured security-authority selection outcomes.");

    /// <summary>Gets the histogram for captured-authority selection duration in seconds.</summary>
    internal static Histogram<double> AuthoritySelectionDuration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.SecurityAuthoritySelectionDuration,
        unit: "s",
        description: "Duration of captured security-authority selection.");

    /// <summary>Records one bounded authority-selection outcome and any trustworthy measured duration.</summary>
    /// <param name="outcome">The defined terminal selection outcome.</param>
    /// <param name="elapsed">A nonnegative duration, or <see langword="null"/> when observation could not measure it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined or <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordAuthoritySelection(SecurityAuthoritySelectionOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }

        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        AuthoritySelections.Add(1, tags);
        if (elapsed is { } measured)
        {
            AuthoritySelectionDuration.Record(measured.TotalSeconds, tags);
        }
    }
}
