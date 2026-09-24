// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Hosts one MCP server endpoint until shutdown.</summary>
/// <remarks>
/// Implementations authenticate peers, map primitives to AgentKit capabilities,
/// and invoke the shared security authority before exposing any effect.
/// </remarks>
public interface IMcpServer
{
    /// <summary>Runs one MCP server endpoint until cancellation or fault.</summary>
    /// <param name="endpoint">The endpoint configuration to serve.</param>
    /// <param name="cancellationToken">A token that cancels the listener.</param>
    /// <returns>A task that completes when the server shuts down.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
    public Task RunAsync(McpServerEndpoint endpoint, CancellationToken cancellationToken);
}
