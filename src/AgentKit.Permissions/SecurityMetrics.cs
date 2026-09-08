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

    /// <summary>Gets the counter for terminal exact security-profile capture outcomes.</summary>
    internal static Counter<long> ProfileCaptures { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityProfileCaptureCount,
        unit: "{capture}",
        description: "Number of terminal exact security-profile capture outcomes.");

    /// <summary>Gets the histogram for exact security-profile capture duration in seconds.</summary>
    internal static Histogram<double> ProfileCaptureDuration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.SecurityProfileCaptureDuration,
        unit: "s",
        description: "Duration of exact security-profile capture.");

    /// <summary>Gets the counter for exact security-profile publication-read outcomes.</summary>
    internal static Counter<long> ProfilePublicationReads { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityProfilePublicationReadCount,
        unit: "{read}",
        description: "Number of exact security-profile publication-read outcomes.");

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

    /// <summary>Gets the counter for terminal security-audit dispatch outcomes.</summary>
    internal static Counter<long> AuditDispatches { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.SecurityAuditDispatchCount,
        unit: "{dispatch}",
        description: "Number of terminal security-audit dispatch outcomes.");

    /// <summary>Gets the histogram for security-audit dispatch duration in seconds.</summary>
    internal static Histogram<double> AuditDispatchDuration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.SecurityAuditDispatchDuration,
        unit: "s",
        description: "Duration of security-audit dispatch.");

    /// <summary>Records one bounded profile-capture outcome and any trustworthy measured duration.</summary>
    /// <param name="outcome">The defined terminal capture outcome.</param>
    /// <param name="elapsed">A nonnegative duration, or <see langword="null"/> when observation could not measure it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined or <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordProfileCapture(SecurityProfileCaptureOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }

        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        ProfileCaptures.Add(1, tags);
        if (elapsed is { } measured)
        {
            ProfileCaptureDuration.Record(measured.TotalSeconds, tags);
        }
    }

    /// <summary>Records one bounded exact publication-read outcome.</summary>
    /// <param name="outcome">The defined terminal lookup outcome.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined.</exception>
    internal static void RecordProfilePublicationRead(SecurityProfilePublicationReadOutcome outcome)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        ProfilePublicationReads.Add(1, tags);
    }

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

    /// <summary>Records one bounded audit-dispatch outcome and any trustworthy measured duration.</summary>
    /// <param name="outcome">The defined terminal audit-dispatch outcome.</param>
    /// <param name="elapsed">A nonnegative duration, or <see langword="null"/> when observation could not measure it.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined or <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordAuditDispatch(SecurityAuditDispatchOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }

        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        AuditDispatches.Add(1, tags);
        if (elapsed is { } measured)
        {
            AuditDispatchDuration.Record(measured.TotalSeconds, tags);
        }
    }
}
