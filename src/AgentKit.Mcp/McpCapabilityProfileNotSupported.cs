// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A capability reference is not the MCP client capability.</summary>
public sealed record McpCapabilityProfileNotSupported: McpCapabilityProfileResolution
{
    /// <summary>Initializes an unsupported-capability resolution.</summary>
    /// <param name="capabilityId">The unsupported capability id.</param>
    /// <exception cref="ArgumentException"><paramref name="capabilityId"/> is default.</exception>
    public McpCapabilityProfileNotSupported(CapabilityId capabilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId.Value, nameof(capabilityId));
        CapabilityId = capabilityId;
    }

    /// <summary>Gets the unsupported capability id.</summary>
    public CapabilityId CapabilityId { get; }
}
