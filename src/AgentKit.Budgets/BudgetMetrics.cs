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

    /// <summary>Increments the terminal reservation count using bounded outcome and registered dimension names.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    /// <param name="dimension">The registered budget dimension; scope and operation identities are intentionally omitted.</param>
    internal static void RecordReservation(string outcome, BudgetDimension dimension) =>
        _reservations.Add(1, new(AgentKitTagNames.Outcome, outcome), new(AgentKitTagNames.BudgetDimension, dimension.ToString()));

    /// <summary>Increments terminal settlement counts using bounded outcome and registered dimension names.</summary>
    /// <param name="outcome">The normalized terminal settlement outcome.</param>
    /// <param name="dimension">The registered budget dimension; reservation identity and amounts are intentionally omitted.</param>
    internal static void RecordSettlement(string outcome, BudgetDimension dimension) =>
        _settlements.Add(1, new(AgentKitTagNames.Outcome, outcome), new(AgentKitTagNames.BudgetDimension, dimension.ToString()));

    /// <summary>Increments start-accounting outcomes using only bounded outcome and dimension tags.</summary>
    /// <param name="outcome">The normalized terminal start outcome.</param>
    /// <param name="dimension">The registered dimension; reservation and scope identities are omitted.</param>
    internal static void RecordStart(string outcome, BudgetDimension dimension) =>
        _starts.Add(1, new(AgentKitTagNames.Outcome, outcome), new(AgentKitTagNames.BudgetDimension, dimension.ToString()));

    /// <summary>Increments correction outcomes using only bounded outcome and dimension tags.</summary>
    /// <param name="outcome">The normalized terminal correction outcome.</param>
    /// <param name="dimension">The registered dimension; revision, amounts, and identities are omitted.</param>
    internal static void RecordCorrection(string outcome, BudgetDimension dimension) =>
        _corrections.Add(1, new(AgentKitTagNames.Outcome, outcome), new(AgentKitTagNames.BudgetDimension, dimension.ToString()));
}
