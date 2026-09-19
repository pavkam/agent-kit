// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp;

/// <summary>
/// Maps the neutral MCP client capability to the endpoint keys a definition
/// may use, without putting those keys on the agent definition.
/// </summary>
/// <remarks>
/// Only <see cref="McpCapabilityIds.Client"/> is accepted. A coincidentally
/// equal profile name for another capability is not MCP configuration. Endpoint
/// keys may be empty when a profile is registered before its endpoints; opening
/// a session still requires the selected endpoint to be listed.
/// </remarks>
public sealed record McpCapabilityProfile
{
    /// <summary>Initializes an MCP client capability profile.</summary>
    /// <param name="capabilityId">Must be <see cref="McpCapabilityIds.Client"/>.</param>
    /// <param name="profileId">The non-default neutral profile id.</param>
    /// <param name="revision">The positive profile generation.</param>
    /// <param name="endpointKeys">The ordered endpoint keys. Empty is valid; default entries are not.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="capabilityId"/> is not the MCP client capability, <paramref name="profileId"/> is default,
    /// or <paramref name="endpointKeys"/> is default or contains a default key.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revision"/> is not positive.</exception>
    public McpCapabilityProfile(
        CapabilityId capabilityId,
        CapabilityProfileId profileId,
        McpCapabilityProfileRevision revision,
        ImmutableArray<McpEndpointKey> endpointKeys)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(capabilityId, McpCapabilityIds.Client, nameof(capabilityId));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        ArgumentOutOfRangeException.ThrowIfLessThan(revision.Value, 1, nameof(revision));
        ArgumentException.ThrowIfDefault(endpointKeys);
        foreach (var endpointKey in endpointKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpointKey.Value, nameof(endpointKeys));
        }

        CapabilityId = capabilityId;
        ProfileId = profileId;
        Revision = revision;
        EndpointKeys = endpointKeys;
    }

    /// <summary>Gets the MCP client capability id.</summary>
    public CapabilityId CapabilityId { get; }

    /// <summary>Gets the neutral profile id a definition references.</summary>
    public CapabilityProfileId ProfileId { get; }

    /// <summary>Gets the profile generation.</summary>
    public McpCapabilityProfileRevision Revision { get; }

    /// <summary>Gets the ordered endpoint keys. Equality compares the sequence.</summary>
    public ImmutableArray<McpEndpointKey> EndpointKeys { get; }

    /// <summary>Compares the capability, profile, revision, and ordered endpoint keys.</summary>
    /// <param name="other">The profile to compare with.</param>
    /// <returns><see langword="true"/> when both profiles bind the same keys in the same order.</returns>
    public bool Equals(McpCapabilityProfile? other) =>
        other is not null
        && CapabilityId == other.CapabilityId
        && ProfileId == other.ProfileId
        && Revision == other.Revision
        && EndpointKeys.SequenceEqual(other.EndpointKeys);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CapabilityId);
        hash.Add(ProfileId);
        hash.Add(Revision);
        foreach (var endpointKey in EndpointKeys)
        {
            hash.Add(endpointKey);
        }

        return hash.ToHashCode();
    }
}
