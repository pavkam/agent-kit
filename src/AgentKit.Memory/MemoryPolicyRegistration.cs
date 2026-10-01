// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Declares one memory policy's identity, policy profile, evaluation order, and lifetime.</summary>
/// <remarks>Policies registered for a policy profile run in ascending order, ties broken by registration order. Identity is unique within a policy profile: registering two different declarations under one identity fails the build.</remarks>
public sealed record MemoryPolicyRegistration
{
    /// <summary>Initializes a validated registration.</summary>
    /// <param name="profile">The policy profile the policy contributes to.</param>
    /// <param name="id">The policy's stable identity, unique within the profile.</param>
    /// <param name="order">The evaluation order; lower values run first.</param>
    /// <param name="lifetime">The service lifetime the policy instance is resolved under.</param>
    /// <exception cref="ArgumentException">The profile key or identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lifetime"/> is undefined.</exception>
    public MemoryPolicyRegistration(MemoryPolicyProfileKey profile, ComponentId id, int order, ServiceLifetime lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile.Value, nameof(profile));
        ArgumentException.ThrowIfNullOrWhiteSpace(id.Value, nameof(id));
        ArgumentOutOfRangeException.ThrowIfUndefined(lifetime);
        Profile = profile;
        Id = id;
        Order = order;
        Lifetime = lifetime;
    }

    /// <summary>Gets the policy profile the policy contributes to.</summary>
    public MemoryPolicyProfileKey Profile { get; }

    /// <summary>Gets the policy's stable identity.</summary>
    public ComponentId Id { get; }

    /// <summary>Gets the evaluation order.</summary>
    public int Order { get; }

    /// <summary>Gets the service lifetime the policy instance is resolved under.</summary>
    public ServiceLifetime Lifetime { get; }
}
