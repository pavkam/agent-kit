// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>Tracks MCP endpoint and profile registrations while the service collection is built.</summary>
internal sealed class McpClientRegistrationState
{
    private readonly Dictionary<string, McpEndpoint> _endpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, McpCapabilityProfile> _profiles = new(StringComparer.Ordinal);

    internal bool TryGetEndpoint(McpEndpointKey key, out McpEndpoint? endpoint) =>
        _endpoints.TryGetValue(key.Value, out endpoint);

    internal void SetEndpoint(McpEndpoint endpoint) => _endpoints[endpoint.Key.Value] = endpoint;

    internal bool TryGetProfile(CapabilityProfileId profileId, out McpCapabilityProfile? profile) =>
        _profiles.TryGetValue(profileId.Value, out profile);

    internal void SetProfile(McpCapabilityProfile profile) => _profiles[profile.ProfileId.Value] = profile;
}
