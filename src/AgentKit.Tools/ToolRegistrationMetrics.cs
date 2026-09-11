// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded instruments for materialized tool-registration selection.</summary>
/// <remarks>Callers isolate initialization and recording failures from selection and provider ownership.</remarks>
internal static class ToolRegistrationMetrics
{
    /// <summary>Gets the completed-selection counter.</summary>
    /// <value>Measurements carry only closed selected, unavailable, cancelled, or failed outcomes.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolRegistrationSelectionCount, "{operation}", "Completed tool registration selections.");
    /// <summary>Gets the selection-duration histogram.</summary>
    /// <value>Elapsed seconds; missing or reversed-clock measurements are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.ToolRegistrationSelectionDuration, "s", "Elapsed tool registration selection time.");
}
