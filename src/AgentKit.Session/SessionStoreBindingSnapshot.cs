// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

/// <summary>Freezes the validated descriptor and store bindings shared by the default catalog and selector.</summary>
/// <remarks>Dependency injection creates one singleton snapshot so each composed store descriptor is read exactly once.</remarks>
internal sealed class SessionStoreBindingSnapshot
{
    /// <summary>Initializes one immutable composition snapshot from the additive store set.</summary>
    /// <param name="stores">The non-null explicitly composed stores to enumerate and capture once.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stores"/> or one of its entries is null.</exception>
    /// <exception cref="ArgumentException">A captured descriptor is null or duplicate store keys are present.</exception>
    public SessionStoreBindingSnapshot(IEnumerable<ISessionStore> stores) =>
        Bindings = SessionStoreBindingFactory.Capture(stores);

    /// <summary>Gets the immutable validated store bindings.</summary>
    /// <value>The complete additive store set with exactly-once captured descriptors.</value>
    internal ImmutableArray<SessionStoreBinding> Bindings { get; }
}
