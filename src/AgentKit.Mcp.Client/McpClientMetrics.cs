// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Owns bounded MCP client metric instruments.</summary>
internal static class McpClientMetrics
{
    private static readonly Counter<long> _operations = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.McpClientOperationCount,
        unit: "{operation}",
        description: "Number of terminal MCP client lifecycle outcomes.");

    /// <summary>Records one terminal MCP client outcome without identities or protocol content.</summary>
    /// <param name="operation">The bounded lifecycle operation.</param>
    /// <param name="outcome">The normalized terminal outcome.</param>
    internal static void Record(string operation, string outcome) =>
        _operations.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.McpOperation, operation),
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
