// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Records bounded execution-lease runtime measurements without payload or identity dimensions.</summary>
internal static class DurableLeaseMetrics
{
    private static readonly Counter<long> _acquisitions = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableLeaseAcquisitionCount, unit: "{acquisition}",
        description: "Number of terminal execution-lease acquisition outcomes.");
    private static readonly Histogram<double> _acquisitionDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.DurableLeaseAcquisitionDuration, "s",
        "Duration of execution-lease acquisition attempts.");
    private static readonly Counter<long> _renewals = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableLeaseRenewalCount, unit: "{renewal}",
        description: "Number of terminal execution-lease renewal outcomes.");
    private static readonly Histogram<double> _renewalDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.DurableLeaseRenewalDuration, "s",
        "Duration of execution-lease renewal attempts.");
    private static readonly Counter<long> _releases = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableLeaseReleaseCount, unit: "{release}",
        description: "Number of execution-lease releases.");

    /// <summary>Records one terminal acquisition outcome and duration using bounded dimensions only.</summary>
    /// <param name="outcome">The finite terminal acquisition outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordAcquisition(DurableLeaseAcquisitionOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _acquisitions.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _acquisitionDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }

    /// <summary>Records one terminal renewal outcome and duration using bounded dimensions only.</summary>
    /// <param name="outcome">The finite terminal renewal outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordRenewal(DurableLeaseRenewalOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _renewals.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _renewalDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }

    /// <summary>Records one lease release.</summary>
    internal static void RecordRelease() => _releases.Add(1);
}
