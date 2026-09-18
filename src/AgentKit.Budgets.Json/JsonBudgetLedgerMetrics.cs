// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json;

/// <summary>Owns bounded measurements emitted by the durable JSON budget-ledger adapter.</summary>
/// <remarks>
/// Both instruments use the shared AgentKit meter and the same budget-ledger metric names every other ledger adapter uses,
/// so a host can compare adapters without a package-local naming dialect. Only the bounded operation and outcome names are
/// attached as dimensions; scope, reservation, tenant, and principal identities are high cardinality and stay on logs and
/// traces.
/// </remarks>
internal static class JsonBudgetLedgerMetrics
{
    /// <summary>Gets the counter for terminal ledger-operation outcomes.</summary>
    /// <value>A process-wide counter carrying only bounded operation and outcome tags.</value>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.BudgetLedgerOperationCount,
        unit: "{operation}",
        description: "Number of terminal JSON budget-ledger operations.");

    /// <summary>Gets the histogram for safely measured ledger-operation duration.</summary>
    /// <value>A process-wide seconds histogram carrying only bounded operation and outcome tags.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.BudgetLedgerOperationDuration,
        unit: "s",
        description: "Duration of authoritative JSON budget-ledger operations.");
}
