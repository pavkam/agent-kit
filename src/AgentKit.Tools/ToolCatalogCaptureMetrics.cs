// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Owns bounded catalog-capture lifetime instruments, isolated from invocation metrics.</summary>
/// <remarks>Callers contain instrument initialization and listener failures so ownership never depends on diagnostics.</remarks>
internal static class ToolCatalogCaptureMetrics
{
    /// <summary>Gets the shared counter of completed capture operations.</summary>
    /// <value>The AgentKit meter counter tagged only with bounded operation and outcome values.</value>
    internal static Counter<long> Count { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ToolCatalogCaptureOperationCount, "{operation}", "Completed retained tool-catalog capture operations.");

    /// <summary>Gets the shared histogram of measured operation duration.</summary>
    /// <value>Elapsed seconds; callers omit unavailable or negative measurements and all content dimensions.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(
        AgentKitMetricNames.ToolCatalogCaptureOperationDuration, "s", "Elapsed time for retained tool-catalog capture operations.");
}
