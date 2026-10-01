// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a target provider or catalog which agents a parent may delegate to.</summary>
public sealed record DelegationDiscoveryRequest
{
    /// <summary>Initializes a validated discovery request.</summary>
    /// <param name="parentAgentId">The delegating agent.</param>
    /// <param name="parentSessionId">The delegating session.</param>
    /// <param name="profile">The captured goal profile.</param>
    /// <exception cref="ArgumentOutOfRangeException">An identity is default.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    public DelegationDiscoveryRequest(AgentId parentAgentId, SessionId parentSessionId, GoalProfileReference profile)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(parentAgentId, default);
        ArgumentOutOfRangeException.ThrowIfEqual(parentSessionId, default);
        ArgumentNullException.ThrowIfNull(profile);
        ParentAgentId = parentAgentId;
        ParentSessionId = parentSessionId;
        Profile = profile;
    }

    /// <summary>Gets the delegating agent.</summary>
    public AgentId ParentAgentId { get; }

    /// <summary>Gets the delegating session.</summary>
    public SessionId ParentSessionId { get; }

    /// <summary>Gets the captured goal profile.</summary>
    public GoalProfileReference Profile { get; }
}
