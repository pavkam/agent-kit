// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes one delegation dispatcher's security audience and lifetime safety.</summary>
public sealed record DelegationDispatcherDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="securityAudience">The component identity the delegation grant must name.</param>
    /// <param name="isSingletonSafe"><see langword="true"/> when one instance may serve concurrent delegations.</param>
    /// <param name="isDurable"><see langword="true"/> when an acknowledged handoff survives process loss.</param>
    /// <exception cref="ArgumentException"><paramref name="securityAudience"/> is blank.</exception>
    public DelegationDispatcherDescriptor(ComponentId securityAudience, bool isSingletonSafe, bool isDurable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
        SecurityAudience = securityAudience;
        IsSingletonSafe = isSingletonSafe;
        IsDurable = isDurable;
    }

    /// <summary>Gets the component identity the delegation grant must name.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets a value indicating whether one instance may serve concurrent delegations.</summary>
    public bool IsSingletonSafe { get; }

    /// <summary>Gets a value indicating whether an acknowledged handoff survives process loss.</summary>
    public bool IsDurable { get; }
}
