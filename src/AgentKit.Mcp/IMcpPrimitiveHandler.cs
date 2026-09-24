// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Handles one inbound MCP primitive request from an authenticated peer.</summary>
public interface IMcpPrimitiveHandler
{
    /// <summary>Handles one MCP request under the shared security authority.</summary>
    /// <param name="peer">The authenticated peer context.</param>
    /// <param name="request">The typed MCP request.</param>
    /// <param name="cancellationToken">A token that cancels handling.</param>
    /// <returns>The terminal MCP response.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="peer"/> or <paramref name="request"/> is null.</exception>
    public ValueTask<McpResponse> HandleAsync(
        McpPeerContext peer,
        McpRequest request,
        CancellationToken cancellationToken);
}
