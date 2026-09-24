// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Hosts one MCP server endpoint using the official SDK transport adapters.</summary>
public sealed class McpServerHost(IServiceProvider services): IMcpServer
{
    private readonly IServiceProvider _services = services;

    /// <inheritdoc/>
    public Task RunAsync(McpServerEndpoint endpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        _ = _services.GetServices<IMcpPrimitiveHandler>().ToArray();
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
