// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Marks one keyed MCP capability profile registration in dependency injection.</summary>
public sealed record McpCapabilityProfileRegistration
{
    /// <summary>Initializes a capability profile registration marker.</summary>
    /// <param name="profileId">The registered profile id.</param>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> is default.</exception>
    public McpCapabilityProfileRegistration(CapabilityProfileId profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ProfileId = profileId;
    }

    /// <summary>Gets the registered profile id.</summary>
    public CapabilityProfileId ProfileId { get; }
}
