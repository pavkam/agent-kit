// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Describes one registered MCP server listener configuration.</summary>
public sealed record McpServerEndpoint
{
    /// <summary>Initializes a server endpoint description.</summary>
    /// <param name="key">The semantic server key.</param>
    /// <param name="transport">The listener transport profile.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> is null.</exception>
    public McpServerEndpoint(McpServerKey key, McpTransportProfile transport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(transport);
        Key = key;
        Transport = transport;
    }

    /// <summary>Gets the semantic server key.</summary>
    public McpServerKey Key { get; }

    /// <summary>Gets the listener transport profile.</summary>
    public McpTransportProfile Transport { get; }
}
