// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Captures the explicitly composed session-store descriptors in deterministic key order.</summary>
/// <remarks>The catalog materializes descriptors once during composition. It does not resolve stores by key or authorize any storage operation.</remarks>
public sealed class DefaultSessionStoreCatalog: ISessionStoreCatalog
{
    private readonly ImmutableArray<SessionStoreDescriptor> _descriptors;

    /// <summary>Initializes a catalog from explicitly injected store implementations.</summary>
    /// <param name="stores">The non-null composed stores to capture once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stores"/> or one of its entries is null.</exception>
    /// <exception cref="ArgumentException">A captured descriptor is null or two captured descriptors reuse a store key.</exception>
    public DefaultSessionStoreCatalog(IEnumerable<ISessionStore> stores)
        : this(new SessionStoreBindingSnapshot(stores))
    {
    }

    /// <summary>Initializes the catalog over the shared immutable composition snapshot.</summary>
    /// <param name="snapshot">The non-null exactly-once captured binding snapshot.</param>
    /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> is null.</exception>
    internal DefaultSessionStoreCatalog(SessionStoreBindingSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _descriptors = [.. snapshot.Bindings
            .Select(static binding => binding.Descriptor)
            .OrderBy(static descriptor => descriptor.Key.Value, StringComparer.Ordinal)];
    }

    /// <inheritdoc/>
    public ImmutableArray<SessionStoreDescriptor> GetDescriptors() => _descriptors;
}
