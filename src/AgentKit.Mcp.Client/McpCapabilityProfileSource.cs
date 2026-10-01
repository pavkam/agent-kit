// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

/// <summary>
/// Reports the MCP capability profiles an application registered, so composition validation can prove an
/// <see cref="AgentCapabilityReference"/> to <see cref="McpCapabilityIds.Client"/> resolves without referencing this package.
/// </summary>
/// <remarks>
/// The source reads only the immutable registration markers collected at composition time. It performs no I/O and
/// probes no endpoint, so it is thread-safe and answers synchronously.
/// </remarks>
/// <param name="registrations">The registration markers, one per registered MCP capability profile.</param>
internal sealed class McpCapabilityProfileSource(IEnumerable<McpCapabilityProfileRegistration> registrations): IAgentCapabilityProfileSource
{
    private readonly HashSet<CapabilityProfileId> _profiles = [.. registrations.Select(static registration => registration.ProfileId)];

    /// <inheritdoc/>
    public CapabilityId CapabilityId => McpCapabilityIds.Client;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="profileId"/> is the default value.</exception>
    public bool Contains(CapabilityProfileId profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        return _profiles.Contains(profileId);
    }
}
