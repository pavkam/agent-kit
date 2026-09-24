// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>An endpoint key resolved to one captured endpoint registration.</summary>
public sealed record McpEndpointResolved: McpEndpointResolution
{
    /// <summary>Initializes a successful endpoint resolution.</summary>
    /// <param name="endpoint">The captured endpoint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is null.</exception>
    public McpEndpointResolved(McpEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        Endpoint = endpoint;
    }

    /// <summary>Gets the captured endpoint.</summary>
    public McpEndpoint Endpoint { get; }
}
