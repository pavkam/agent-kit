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
}
