// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Holds the instrumentation helpers the memory coordinator, pipeline, and runtime share.</summary>
/// <remarks>Instrumentation is observational only: every listener, logger, or clock failure is swallowed and never changes an operation's semantic result.</remarks>
internal static class MemoryObservation
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.MemoryOperationCount, unit: "{operation}", description: "Number of terminal memory coordinator outcomes.");

    private static readonly Histogram<double> _operationDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.MemoryOperationDuration, "s", "Duration of memory coordinator operations.");

    private static readonly Counter<long> _retrievals = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.RetrievalCount, unit: "{retrieval}", description: "Number of terminal retrieval outcomes.");

    private static readonly Histogram<double> _retrievalDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.RetrievalDuration, "s", "Duration of retrieval pipeline runs.");

    private static readonly Histogram<long> _candidates = AgentKitDiagnostics.Metrics.CreateHistogram<long>(
        AgentKitMetricNames.RetrievalCandidates, "{candidate}", "Candidates one retrieval exposed.");

    private static readonly Counter<long> _omitted = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.RetrievalOmittedCount, unit: "{candidate}", description: "Candidates a retrieval omitted.");

    /// <summary>Runs one observation and swallows any failure so instrumentation never changes a semantic result.</summary>
    /// <param name="observation">The observation to run.</param>
    internal static void Safe(Action observation)
    {
        try { observation(); } catch (Exception) { /* Observation never changes the semantic result. */ }
    }

    /// <summary>Records one terminal coordinator operation.</summary>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="elapsed">The measured duration, or <see langword="null"/>.</param>
    internal static void RecordOperation(string operation, string outcome, TimeSpan? elapsed)
    {
        TagList tags = default;
        tags.Add(AgentKitTagNames.MemoryStoreOperation, operation);
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _operations.Add(1, tags);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _operationDuration.Record(duration.TotalSeconds, tags);
        }
    }

    /// <summary>Records one terminal retrieval.</summary>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="elapsed">The measured duration, or <see langword="null"/>.</param>
    /// <param name="exposed">The candidates exposed, or <see langword="null"/> when the retrieval failed.</param>
    /// <param name="summary">The omission summary, or <see langword="null"/> when the retrieval failed.</param>
    internal static void RecordRetrieval(string outcome, TimeSpan? elapsed, int? exposed, RetrievalSummary? summary)
    {
        TagList tags = default;
        tags.Add(AgentKitTagNames.Outcome, outcome);
        _retrievals.Add(1, tags);
        if (elapsed is { } duration && duration >= TimeSpan.Zero)
        {
            _retrievalDuration.Record(duration.TotalSeconds, tags);
        }

        if (exposed is { } count)
        {
            _candidates.Record(count);
        }

        if (summary is not null)
        {
            Omit("stale", summary.OmittedStale);
            Omit("unauthorized", summary.OmittedUnauthorized);
            Omit("duplicate", summary.OmittedDuplicate);
            Omit("budget", summary.OmittedByBudget);
        }
    }

    private static void Omit(string reason, int count)
    {
        if (count > 0)
        {
            TagList tags = default;
            tags.Add(AgentKitTagNames.RetrievalOmissionReason, reason);
            _omitted.Add(count, tags);
        }
    }

    /// <summary>Gets the clock timestamp, or <see langword="null"/> when the clock faults.</summary>
    /// <param name="time">The clock.</param>
    /// <returns>The timestamp, or <see langword="null"/>.</returns>
    internal static long? TryTimestamp(TimeProvider time)
    {
        try { return time.GetTimestamp(); } catch (Exception) { return null; }
    }

    /// <summary>Gets the elapsed time since a timestamp, or <see langword="null"/> when the clock faults.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="started">The start timestamp.</param>
    /// <returns>The elapsed time, or <see langword="null"/>.</returns>
    internal static TimeSpan? TryElapsed(TimeProvider time, long? started)
    {
        if (started is not { } timestamp)
        {
            return null;
        }

        try { return time.GetElapsedTime(timestamp); } catch (Exception) { return null; }
    }
}
