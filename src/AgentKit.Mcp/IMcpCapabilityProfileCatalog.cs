// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Resolves neutral agent capability references to MCP-owned profiles.</summary>
public interface IMcpCapabilityProfileCatalog
{
    /// <summary>Resolves one capability reference to an MCP client profile.</summary>
    /// <param name="capability">The neutral capability reference from an agent definition.</param>
    /// <param name="cancellationToken">A token that cancels resolution.</param>
    /// <returns>The terminal resolution outcome.</returns>
    public ValueTask<McpCapabilityProfileResolution> ResolveAsync(
        AgentCapabilityReference capability,
        CancellationToken cancellationToken);
}
