// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Lazily registers shared hub instruments after measurement arguments have passed validation.</summary>
/// <remarks>This type is first accessed inside the caller's isolated observation boundary, never during queue mutation.</remarks>
internal static class RunEventHubInstruments
{
    /// <summary>Gets the process-wide count instrument using only bounded operation and outcome dimensions.</summary>
    /// <value>The shared exporter-free operation counter.</value>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.RunEventHubOperationCount);

    /// <summary>Gets the process-wide duration instrument with seconds as its unit.</summary>
    /// <value>The shared exporter-free histogram; unavailable or negative clock samples are omitted.</value>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.RunEventHubOperationDuration, "s");
}
