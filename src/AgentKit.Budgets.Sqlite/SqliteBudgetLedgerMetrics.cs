// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Publishes bounded aggregate SQLite ledger operation evidence.</summary>
internal static class SqliteBudgetLedgerMetrics
{
    private static readonly Counter<long> _count = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.BudgetLedgerOperationCount);
    private static readonly Histogram<double> _duration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.BudgetLedgerOperationDuration, "s");

    /// <summary>Records one terminal operation count.</summary><param name="operation">The bounded operation.</param><param name="outcome">The bounded outcome.</param>
    /// <exception cref="ArgumentException">A name is blank.</exception>
    internal static void RecordCount(string operation, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        TagList tags = new() { { AgentKitTagNames.BudgetOperation, operation }, { AgentKitTagNames.Outcome, outcome } };
        _count.Add(1, tags);
    }

    /// <summary>Records one nonnegative injected-clock duration.</summary><param name="operation">The bounded operation.</param>
    /// <param name="outcome">The bounded outcome.</param><param name="duration">The nonnegative elapsed duration.</param>
    /// <exception cref="ArgumentException">A name is blank.</exception><exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
    internal static void RecordDuration(string operation, string outcome, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        TagList tags = new() { { AgentKitTagNames.BudgetOperation, operation }, { AgentKitTagNames.Outcome, outcome } };
        _duration.Record(duration.TotalSeconds, tags);
    }
}
