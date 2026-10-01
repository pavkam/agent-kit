// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IAgentCapabilityProfileSource"/> test double that answers for one capability and a fixed profile set.</summary>
/// <remarks>Initialized with the capability it owns and the profiles it reports as registered.</remarks>
public sealed class StaticAgentCapabilityProfileSource: IAgentCapabilityProfileSource
{
    private readonly HashSet<CapabilityProfileId> _profiles;

    /// <summary>Initializes the source.</summary>
    /// <param name="capabilityId">The nonblank capability this source owns.</param>
    /// <param name="profiles">The profile identities reported as registered; may be empty.</param>
    /// <exception cref="ArgumentException"><paramref name="capabilityId"/> is the default value.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="profiles"/> is null.</exception>
    public StaticAgentCapabilityProfileSource(CapabilityId capabilityId, params CapabilityProfileId[] profiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId.Value, nameof(capabilityId));
        ArgumentNullException.ThrowIfNull(profiles);
        CapabilityId = capabilityId;
        _profiles = [.. profiles];
    }

    /// <inheritdoc/>
    public CapabilityId CapabilityId { get; }

    /// <inheritdoc/>
    public bool Contains(CapabilityProfileId profileId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId.Value, nameof(profileId));
        return _profiles.Contains(profileId);
    }
}
