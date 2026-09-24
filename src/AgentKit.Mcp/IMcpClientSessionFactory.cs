// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Opens MCP client sessions from captured open requests.</summary>
/// <remarks>
/// Implementations are normally singletons, retain no caller context between
/// calls, and validate each <see cref="McpClientOpenRequest"/> before transport
/// activation.
/// </remarks>
public interface IMcpClientSessionFactory
{
    /// <summary>Opens one MCP client session.</summary>
    /// <param name="request">The captured open request.</param>
    /// <param name="cancellationToken">A token that cancels opening.</param>
    /// <returns>The opened session owned by the caller.</returns>
    public ValueTask<IMcpClientSession> OpenAsync(McpClientOpenRequest request, CancellationToken cancellationToken);
}
