// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Configures one MCP client capability profile before it is captured immutably.</summary>
public sealed class McpCapabilityProfileOptions
{
    /// <summary>Gets the endpoint keys exposed through this profile in registration order.</summary>
    public IList<McpEndpointKey> EndpointKeys { get; } = [];
}
