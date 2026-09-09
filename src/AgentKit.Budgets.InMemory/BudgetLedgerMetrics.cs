// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.InMemory;

/// <summary>Records bounded terminal counts and durations for budget-ledger operations.</summary>
internal static class BudgetLedgerMetrics
{
    private static readonly Counter<long> _count = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.BudgetLedgerOperationCount);
    private static readonly Histogram<double> _duration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.BudgetLedgerOperationDuration, "s");

    /// <summary>Records one terminal operation without identity or content dimensions.</summary>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <exception cref="ArgumentException"><paramref name="operation"/> or <paramref name="outcome"/> is null, empty, or whitespace.</exception>
    internal static void RecordCount(string operation, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        var tags = new TagList
        {
            { AgentKitTagNames.BudgetOperation, operation },
            { AgentKitTagNames.Outcome, outcome },
        };
        _count.Add(1, tags);
    }

    /// <summary>Records elapsed duration when the injected clock supplied valid timing evidence.</summary>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="duration">The nonnegative injected-clock elapsed duration.</param>
    /// <exception cref="ArgumentException"><paramref name="operation"/> or <paramref name="outcome"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
    internal static void RecordDuration(string operation, string outcome, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        var tags = new TagList
        {
            { AgentKitTagNames.BudgetOperation, operation },
            { AgentKitTagNames.Outcome, outcome },
        };
        _duration.Record(duration.TotalSeconds, tags);
    }
}
