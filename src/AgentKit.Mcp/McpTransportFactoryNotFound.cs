// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>No transport factory is registered for the requested profile kind.</summary>
public sealed record McpTransportFactoryNotFound: McpTransportFactoryResolution
{
    /// <summary>Initializes a not-found factory resolution.</summary>
    /// <param name="transportProfileType">The requested transport profile runtime type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transportProfileType"/> is null.</exception>
    public McpTransportFactoryNotFound(Type transportProfileType)
    {
        ArgumentNullException.ThrowIfNull(transportProfileType);
        TransportProfileType = transportProfileType;
    }

    /// <summary>Gets the requested transport profile runtime type.</summary>
    public Type TransportProfileType { get; }
}
