// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Marks one keyed MCP endpoint registration in dependency injection.</summary>
public sealed record McpEndpointRegistration
{
    /// <summary>Initializes an endpoint registration marker.</summary>
    /// <param name="key">The registered endpoint key.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default.</exception>
    public McpEndpointRegistration(McpEndpointKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        Key = key;
    }

    /// <summary>Gets the registered endpoint key.</summary>
    public McpEndpointKey Key { get; }
}
