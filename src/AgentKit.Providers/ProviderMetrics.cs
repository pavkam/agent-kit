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

    private static readonly Counter<long> _executions = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ModelExecutionCount,
        unit: "{execution}",
        description: "Number of terminal model-execution outcomes.");

    private static readonly Counter<long> _providerRequests = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ProviderRequestCount,
        unit: "{request}",
        description: "Number of terminal provider HTTP request outcomes.");

    private static readonly Histogram<double> _providerRequestDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ProviderRequestDuration,
        unit: "s",
        description: "Duration of terminal provider HTTP requests in seconds.");

    private static readonly Counter<long> _providerEgress = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ProviderEgressCount,
        unit: "{request}",
        description: "Number of terminal provider egress boundary outcomes.");

    private static readonly Histogram<double> _providerEgressDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ProviderEgressDuration,
        unit: "s",
        description: "Duration of provider egress boundary crossings in seconds, through response headers.");

    private static readonly Counter<long> _providerCredentialRead = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ProviderCredentialReadCount,
        unit: "{read}",
        description: "Number of terminal provider credential-read outcomes.");

    private static readonly Histogram<double> _providerCredentialReadDuration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ProviderCredentialReadDuration,
        unit: "s",
        description: "Duration of provider credential reads in seconds, through lease application.");

    /// <summary>Records one provider credential-read outcome using the bounded outcome only.</summary>
    /// <param name="outcome">The bounded terminal outcome: <c>released</c>, <c>cancelled</c>, or a lowercase failure kind.</param>
    /// <param name="duration">The elapsed time from grant request through lease application or refusal.</param>
    internal static void RecordCredentialRead(string outcome, TimeSpan duration)
    {
        _providerCredentialRead.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        _providerCredentialReadDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Records one provider egress outcome using bounded dimensions only.</summary>
    /// <param name="operation">The bounded provider operation tag.</param>
    /// <param name="outcome">The bounded terminal outcome: <c>sent</c> or a lowercase failure kind.</param>
    /// <param name="duration">The elapsed time through response headers or refusal.</param>
    internal static void RecordEgress(string operation, string outcome, TimeSpan duration)
    {
        _providerEgress.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.ProviderOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        _providerEgressDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(AgentKitTagNames.ProviderOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }

    /// <summary>Records one catalog refresh using a bounded outcome only.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void RecordCatalogRefresh(string outcome) =>
        _catalogRefreshes.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Records one model selection using a bounded outcome only.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void RecordSelection(string outcome) =>
        _selections.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Records one model execution using a bounded outcome only.</summary>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void RecordExecution(string outcome) =>
        _executions.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));

    /// <summary>Records one provider request using bounded operation and outcome dimensions.</summary>
    internal static void RecordProviderRequest(string operation, string outcome, TimeSpan duration)
    {
        _providerRequests.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.ProviderOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        _providerRequestDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>(AgentKitTagNames.ProviderOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
    }
}
