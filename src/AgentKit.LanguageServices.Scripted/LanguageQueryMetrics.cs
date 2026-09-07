// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

/// <summary>Owns the bounded language-query outcome counter.</summary>
internal static class LanguageQueryMetrics
{
    private static readonly Counter<long> _queries = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.LanguageQueryCount,
        unit: "{query}",
        description: "Number of terminal language-intelligence query outcomes.");

    /// <summary>Records one terminal query with bounded kind and status dimensions.</summary>
    /// <param name="kind">The bounded language-query kind.</param>
    /// <param name="outcome">The normalized terminal status.</param>
    internal static void Record(LanguageQueryKind kind, string outcome) =>
        _queries.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.LanguageQueryKind, kind.ToString()),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
