// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the identity and guarantees of one keyed memory store.</summary>
/// <remarks>Capability claims describe what the adapter actually provides. A store that is not durable says so here, so composition and policy never assume otherwise.</remarks>
public sealed record MemoryStoreDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="key">The key the store is registered and selected under.</param>
    /// <param name="name">The non-blank adapter name.</param>
    /// <param name="securityAudience">The component identity that grants for this store must name.</param>
    /// <param name="isDurable"><see langword="true"/> when an acknowledged write survives process loss.</param>
    /// <exception cref="ArgumentException">A text value is blank.</exception>
    public MemoryStoreDescriptor(MemoryStoreKey key, string name, ComponentId securityAudience, bool isDurable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(securityAudience.Value, nameof(securityAudience));
        Key = key;
        Name = name;
        SecurityAudience = securityAudience;
        IsDurable = isDurable;
    }

    /// <summary>Gets the key the store is registered under.</summary>
    public MemoryStoreKey Key { get; }

    /// <summary>Gets the adapter name.</summary>
    public string Name { get; }

    /// <summary>Gets the component identity grants must name.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets a value indicating whether an acknowledged write survives process loss.</summary>
    public bool IsDurable { get; }
}
