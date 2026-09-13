// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Records bounded durable-journal runtime measurements without payload or identity dimensions.</summary>
internal static class DurableJournalMetrics
{
    private static readonly Counter<long> _writes = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableJournalWriteCount, unit: "{write}",
        description: "Number of terminal durable-journal write outcomes.");
    private static readonly Histogram<double> _writeDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.DurableJournalWriteDuration, "s",
        "Duration of durable-journal write attempts.");
    private static readonly Counter<long> _evidenceLoads = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.DurableJournalLoadEvidenceCount, unit: "{load}",
        description: "Number of terminal durable-journal evidence-load outcomes.");
    private static readonly Histogram<double> _evidenceLoadDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.DurableJournalLoadEvidenceDuration, "s",
        "Duration of durable-journal evidence-load attempts.");

    /// <summary>Records one terminal write outcome and duration using bounded dimensions only.</summary>
    /// <param name="operation">The finite write-stage dimension.</param>
    /// <param name="outcome">The finite terminal write outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operation"/> or <paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordWrite(DurableJournalWriteOperation operation, DurableJournalWriteOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(operation);
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }
        TagList tags = default;
        tags.Add(AgentKitTagNames.DurableJournalOperation, operation.ToStableValue());
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _writes.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _writeDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }

    /// <summary>Records one terminal evidence-load outcome and duration using bounded dimensions only.</summary>
    /// <param name="outcome">The finite terminal evidence-load outcome.</param>
    /// <param name="elapsed">The nonnegative elapsed duration, or null when the injected clock could not produce one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="outcome"/> is undefined, or a present <paramref name="elapsed"/> is negative.</exception>
    internal static void RecordEvidenceLoad(DurableJournalEvidenceOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed));
        }
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome.ToStableValue());
        _evidenceLoads.Add(1, tags);
        if (elapsed is { } measuredDuration)
        {
            _evidenceLoadDuration.Record(measuredDuration.TotalSeconds, tags);
        }
    }
}
