// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Defines bounded schema operation instruments on the shared AgentKit meter.</summary>
internal static class ToolSchemaMetrics
{
    /// <summary>Counts complete local schema operations using only operation and outcome dimensions.</summary>
    internal static readonly Counter<long> Count = AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.ToolSchemaOperationCount);
    /// <summary>Measures valid elapsed schema-operation seconds without identity or content dimensions.</summary>
    internal static readonly Histogram<double> Duration = AgentKitDiagnostics.Metrics.CreateHistogram<double>(AgentKitMetricNames.ToolSchemaOperationDuration, "s");
}
