// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Records bounded aggregate dimensions for local run-event fan-out.</summary>
internal static class RunEventHubMetrics
{
    /// <summary>Records one operation outcome and an optional trustworthy duration.</summary>
    /// <param name="operation">The defined operation dimension.</param>
    /// <param name="outcome">The defined outcome dimension.</param>
    /// <param name="elapsed">A nonnegative duration, or null when unavailable.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum is undefined or the duration is negative.</exception>
    internal static void Record(RunEventHubOperation operation, RunEventHubOutcome outcome, TimeSpan? elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(operation);
        ArgumentOutOfRangeException.ThrowIfUndefined(outcome);
        if (elapsed is { } duration) { ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero, nameof(elapsed)); }
        TagList tags = default;
        tags.Add(AgentKitTagNames.RunEventHubOperation, operation.ToString());
        tags.Add(AgentKitTagNames.Outcome, outcome.ToString());
        try { RunEventHubInstruments.Operations.Add(1, tags); }
        finally { if (elapsed is { } measured) { RunEventHubInstruments.Duration.Record(measured.TotalSeconds, tags); } }
    }
}
