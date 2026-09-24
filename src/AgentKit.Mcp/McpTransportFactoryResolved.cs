// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A transport profile resolved to one factory.</summary>
public sealed record McpTransportFactoryResolved: McpTransportFactoryResolution
{
    /// <summary>Initializes a successful factory resolution.</summary>
    /// <param name="factory">The resolved transport factory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
    public McpTransportFactoryResolved(IMcpTransportFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Factory = factory;
    }

    /// <summary>Gets the resolved transport factory.</summary>
    public IMcpTransportFactory Factory { get; }
}
