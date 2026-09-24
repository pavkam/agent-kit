// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Resolves configured MCP endpoints by semantic key.</summary>
public interface IMcpEndpointCatalog
{
    /// <summary>Resolves one endpoint key to a captured endpoint registration.</summary>
    /// <param name="key">The endpoint key to resolve.</param>
    /// <param name="cancellationToken">A token that cancels resolution.</param>
    /// <returns>The terminal resolution outcome.</returns>
    public ValueTask<McpEndpointResolution> ResolveAsync(McpEndpointKey key, CancellationToken cancellationToken);
}
