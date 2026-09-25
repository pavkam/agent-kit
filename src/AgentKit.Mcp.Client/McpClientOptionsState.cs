// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Holds the captured client options snapshot for endpoint registration.</summary>
internal sealed class McpClientOptionsState
{
    internal McpClientOptionsSnapshot Snapshot { get; private set; } = CreateSnapshot(new McpClientOptions());

    internal void Configure(Action<McpClientOptions>? configure)
    {
        var options = new McpClientOptions();
        configure?.Invoke(options);
        McpClientRegistration.ValidateClientOptions(options);
        Snapshot = CreateSnapshot(options);
    }

    private static McpClientOptionsSnapshot CreateSnapshot(McpClientOptions options) => new(
        options.HandshakeTimeout,
        options.RequestTimeout,
        options.ShutdownTimeout,
        options.MaximumFrameBytes,
        options.MaximumMessageBytes,
        options.MaximumInFlightRequests,
        options.UnknownNotificationPolicy,
        options.StdioProcessExecutorKey,
        options.StdioSandboxProfileId);
}
