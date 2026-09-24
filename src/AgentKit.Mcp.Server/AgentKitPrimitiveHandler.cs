// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Server;

/// <summary>Maps inbound MCP tool requests onto the shared tool executor.</summary>
public sealed class AgentKitPrimitiveHandler(IToolExecutor executor): IMcpPrimitiveHandler
{
    private readonly IToolExecutor _executor = executor;

    /// <inheritdoc/>
    public ValueTask<McpResponse> HandleAsync(
        McpPeerContext peer,
        McpRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(request);
        return request switch
        {
            McpToolsCallRequest toolCall => HandleToolCallAsync(peer, toolCall, cancellationToken),
            _ => ValueTask.FromResult<McpResponse>(
                new McpResponseUnsupportedCapability(request.Id, request.GetType().Name)),
        };
    }

    private async ValueTask<McpResponse> HandleToolCallAsync(
        McpPeerContext peer,
        McpToolsCallRequest request,
        CancellationToken cancellationToken)
    {
        _ = peer;
        _ = _executor;
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return new McpResponseUnsupportedCapability(
            request.Id,
            "AgentKit MCP server tool dispatch is not yet wired to IToolExecutor in this build.");
    }
}
