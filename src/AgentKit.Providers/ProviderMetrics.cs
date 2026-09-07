// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>Owns bounded provider-neutral model catalog and selection metric instruments.</summary>
internal static class ProviderMetrics
{
    private static readonly Counter<long> _catalogRefreshes = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ModelCatalogRefreshCount,
        unit: "{refresh}",
        description: "Number of terminal model-catalog refresh outcomes.");

    private static readonly Counter<long> _selections = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ModelSelectionCount,
        unit: "{selection}",
        description: "Number of terminal model-selection outcomes.");

    /// <summary>Records one catalog refresh using a bounded outcome only.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void RecordCatalogRefresh(string outcome) =>
        _catalogRefreshes.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Records one model selection using a bounded outcome only.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void RecordSelection(string outcome) =>
        _selections.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
