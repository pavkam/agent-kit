// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets;

/// <summary>Records bounded budget outcome metrics without scope or operation identities.</summary>
internal static class BudgetMetrics
{
    private static readonly Counter<long> _reservations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetReservationCount);
    private static readonly Counter<long> _settlements = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetSettlementCount);
    private static readonly Counter<long> _starts = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetStartCount);
    private static readonly Counter<long> _corrections = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetCorrectionCount);
    private static readonly Counter<long> _scopeCreates = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetScopeCreateCount);
    private static readonly Counter<long> _snapshots = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetSnapshotCount);

    /// <summary>Increments the terminal reservation count using only a bounded outcome.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void RecordReservation(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _reservations.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Increments terminal settlement counts using only a bounded outcome.</summary>
    /// <param name="outcome">The normalized terminal settlement outcome.</param>
    internal static void RecordSettlement(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _settlements.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Increments start-accounting outcomes using only a bounded outcome.</summary>
    /// <param name="outcome">The normalized terminal start outcome.</param>
    internal static void RecordStart(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _starts.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Increments correction outcomes using only a bounded outcome.</summary>
    /// <param name="outcome">The normalized terminal correction outcome.</param>
    internal static void RecordCorrection(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _corrections.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Increments scope-creation outcomes without identity or caller-controlled dimensions.</summary>
    /// <param name="outcome">The bounded terminal creation outcome.</param>
    internal static void RecordScopeCreate(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _scopeCreates.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Increments snapshot-read outcomes without identity or caller-controlled dimensions.</summary>
    /// <param name="outcome">The bounded terminal read outcome.</param>
    internal static void RecordSnapshot(string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        _snapshots.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }
}
