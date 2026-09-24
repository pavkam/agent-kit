// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A transport was opened and returned to the caller.</summary>
public sealed record McpTransportOpened: McpTransportOpenResult
{
    /// <summary>Initializes a successful transport open.</summary>
    /// <param name="transport">The opened transport owned by the caller until disposal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> is null.</exception>
    public McpTransportOpened(IMcpTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        Transport = transport;
    }

    /// <summary>Gets the opened transport.</summary>
    public IMcpTransport Transport { get; }
}
