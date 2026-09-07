// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Owns bounded context-preparation metric instruments.</summary>
internal static class ContextMetrics
{
    /// <summary>Gets the process-wide counter for terminal preparation outcomes.</summary>
    internal static Counter<long> Preparations { get; } = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.ContextPreparationCount,
        unit: "{preparation}",
        description: "Number of terminal context-preparation outcomes.");
}
