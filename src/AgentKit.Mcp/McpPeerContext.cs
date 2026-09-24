// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>Captures authenticated peer evidence for one MCP server connection.</summary>
public sealed record McpPeerContext
{
    /// <summary>Initializes peer context with a validated server key.</summary>
    /// <param name="serverKey">The non-default server key.</param>
    /// <param name="peerIdentity">Optional untrusted peer display metadata.</param>
    /// <exception cref="ArgumentException"><paramref name="serverKey"/> is uninitialized.</exception>
    public McpPeerContext(McpServerKey serverKey, string? peerIdentity = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverKey.Value, nameof(serverKey));
        ServerKey = serverKey;
        PeerIdentity = peerIdentity;
    }

    /// <summary>Gets the configured server key serving this connection.</summary>
    public McpServerKey ServerKey { get; }

    /// <summary>Gets optional display metadata supplied by the peer.</summary>
    /// <remarks>Peer metadata never grants authority.</remarks>
    public string? PeerIdentity { get; }
}
