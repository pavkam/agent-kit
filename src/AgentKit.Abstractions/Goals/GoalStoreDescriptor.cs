// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the identity and guarantees of one goal store implementation.</summary>
/// <remarks>Capability claims describe what the adapter actually provides. A store that is not durable, or cannot discover intents across sessions, says so here so composition and workers never assume otherwise.</remarks>
public sealed record GoalStoreDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="name">The non-blank adapter name.</param>
    /// <param name="securityAudience">The component identity that grants for this store must name.</param>
    /// <param name="isDurable"><see langword="true"/> when an acknowledged write survives process loss.</param>
    /// <param name="supportsIntentDiscovery"><see langword="true"/> when <see cref="IGoalStore.ReadIntentsAsync"/> can enumerate open delegated children across sessions.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="securityAudience"/> is blank.</exception>
    public GoalStoreDescriptor(string name, ComponentId securityAudience, bool isDurable, bool supportsIntentDiscovery)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
        Name = name;
        SecurityAudience = securityAudience;
        IsDurable = isDurable;
        SupportsIntentDiscovery = supportsIntentDiscovery;
    }

    /// <summary>Gets the adapter name.</summary>
    public string Name { get; }

    /// <summary>Gets the component identity grants must name.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets a value indicating whether an acknowledged write survives process loss.</summary>
    public bool IsDurable { get; }

    /// <summary>Gets a value indicating whether the store can enumerate open delegated children across sessions.</summary>
    public bool SupportsIntentDiscovery { get; }
}
