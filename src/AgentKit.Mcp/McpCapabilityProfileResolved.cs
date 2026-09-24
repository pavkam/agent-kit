// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>A capability reference resolved to one MCP client profile.</summary>
public sealed record McpCapabilityProfileResolved: McpCapabilityProfileResolution
{
    /// <summary>Initializes a successful profile resolution.</summary>
    /// <param name="profile">The captured MCP capability profile.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public McpCapabilityProfileResolved(McpCapabilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile;
    }

    /// <summary>Gets the captured MCP capability profile.</summary>
    public McpCapabilityProfile Profile { get; }
}
