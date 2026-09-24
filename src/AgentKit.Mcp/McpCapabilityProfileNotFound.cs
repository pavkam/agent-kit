// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A capability profile id is not registered in the MCP catalog.</summary>
public sealed record McpCapabilityProfileNotFound: McpCapabilityProfileResolution
{
    /// <summary>Initializes a not-found profile resolution.</summary>
    /// <param name="profileId">The requested profile id.</param>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> is default.</exception>
    public McpCapabilityProfileNotFound(CapabilityProfileId profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ProfileId = profileId;
    }

    /// <summary>Gets the requested profile id.</summary>
    public CapabilityProfileId ProfileId { get; }
}
