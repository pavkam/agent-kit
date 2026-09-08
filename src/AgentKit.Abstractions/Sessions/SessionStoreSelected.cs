// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports the one explicitly composed store selected for an operation.</summary>
/// <remarks>The coordinator must separately obtain a store-specific grant before invoking the selected store.</remarks>
public sealed record SessionStoreSelected: SessionStoreSelectionResult
{
    /// <summary>Initializes a successful store-selection outcome.</summary>
    /// <param name="store">The non-null explicitly composed store.</param>
    /// <param name="descriptor">The non-null descriptor captured when the store set was composed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="descriptor"/> is null.</exception>
    public SessionStoreSelected(ISessionStore store, SessionStoreDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(descriptor);
        Store = store;
        Descriptor = descriptor;
    }

    /// <summary>Gets the selected store.</summary><value>The non-null store; this outcome grants no access authority.</value>
    public ISessionStore Store { get; }
    /// <summary>Gets the descriptor captured with the selected store.</summary><value>The immutable descriptor used for selection, not a later mutable store property read.</value>
    public SessionStoreDescriptor Descriptor { get; }
}
