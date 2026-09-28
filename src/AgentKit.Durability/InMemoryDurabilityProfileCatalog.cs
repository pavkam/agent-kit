// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using System.Diagnostics.CodeAnalysis;

/// <summary>An <see cref="IDurabilityProfileCatalog"/> over profiles accumulated from registered contributors.</summary>
/// <remarks>
/// The catalog is a process-local immutable projection of composition-time registration, not a persistence adapter.
/// Reads are safe for concurrent use and never activate a backend, journal, lease manager, or recovery policy.
/// </remarks>
internal sealed class InMemoryDurabilityProfileCatalog: IDurabilityProfileCatalog
{
    private readonly DurabilityProfileRegistry _registry;

    /// <summary>Initializes the catalog over a registry already populated from every profile contributor.</summary>
    /// <param name="registry">The non-null initialized profile registry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public InMemoryDurabilityProfileCatalog(DurabilityProfileRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <inheritdoc/>
    public bool TryGet(DurabilityProfileKey key, [NotNullWhen(true)] out DurabilityProfileSnapshot? profile) =>
        _registry.TryGet(key, out profile);
}
