// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Materializes validated immutable bindings for the explicitly injected session-store set.</summary>
/// <remarks>The factory enumerates the supplied composition collection once, captures every descriptor once, validates every captured descriptor before checking keys, and performs no service resolution or storage access.</remarks>
internal static class SessionStoreBindingFactory
{
    /// <summary>Captures the complete explicit session-store composition.</summary>
    /// <param name="stores">The non-null store sequence supplied by dependency injection.</param>
    /// <returns>Immutable bindings retaining each store and its exactly-once captured descriptor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stores"/> or one of its elements is null.</exception>
    /// <exception cref="ArgumentException">A captured descriptor is null or two captured descriptors reuse a store key.</exception>
    internal static ImmutableArray<SessionStoreBinding> Capture(IEnumerable<ISessionStore> stores)
    {
        ArgumentNullException.ThrowIfNull(stores);
        var materialized = stores.ToImmutableArray();
        ArgumentException.ThrowIfContainsNull(materialized, nameof(stores));
        var bindings = ImmutableArray.CreateBuilder<SessionStoreBinding>(materialized.Length);
        foreach (var store in materialized)
        {
            var descriptor = store.Descriptor;
            ArgumentException.ThrowIfNullSessionStoreDescriptor(descriptor, nameof(stores));
            bindings.Add(new SessionStoreBinding(store, descriptor));
        }

        var captured = bindings.MoveToImmutable();
        ArgumentException.ThrowIfDuplicateSessionStoreBindingKeys(captured, nameof(stores));
        return captured;
    }
}
