// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Owns bounded metric instruments for session-entry codec operations.</summary>
internal static class SessionEntryCodecMetrics
{
    /// <summary>Gets the counter for terminal codec outcomes.</summary>
    internal static Counter<long> Operations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.SessionEntryCodecCount);

    /// <summary>Gets the histogram for codec operation duration.</summary>
    internal static Histogram<double> Duration { get; } = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.SessionEntryCodecDuration, unit: "s");
}
