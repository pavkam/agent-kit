// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>An endpoint key is not registered in the catalog.</summary>
public sealed record McpEndpointNotFound: McpEndpointResolution
{
    /// <summary>Initializes a not-found endpoint resolution.</summary>
    /// <param name="key">The requested endpoint key.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default.</exception>
    public McpEndpointNotFound(McpEndpointKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        Key = key;
    }

    /// <summary>Gets the requested endpoint key.</summary>
    public McpEndpointKey Key { get; }
}
