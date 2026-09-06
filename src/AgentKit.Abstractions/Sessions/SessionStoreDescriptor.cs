// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one registered <see cref="ISessionStore"/> implementation and
/// its durability guarantee.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. This is a
/// deliberately reduced stand-in for the fuller store descriptor described
/// by the sessions architecture, which additionally advertises store
/// capabilities, consistency model, and distributed-fencing support once
/// multiple concurrently registered stores exist; today's single-store
/// composition only needs to know which key a store answers to and whether
/// it survives a process restart.
/// </remarks>
public sealed record SessionStoreDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="SessionStoreDescriptor"/> record.</summary>
    /// <param name="key">The store's registered key.</param>
    /// <param name="durable">
    /// <see langword="true"/> if the store persists its record beyond the
    /// current process; <see langword="false"/> for an ephemeral,
    /// in-memory-only store.
    /// </param>
    public SessionStoreDescriptor(SessionStoreKey key, bool durable)
    {
        Key = key;
        Durable = durable;
    }

    /// <summary>Gets the store's registered key.</summary>
    public SessionStoreKey Key { get; init; }

    /// <summary>
    /// Gets a value indicating whether the store persists its record beyond
    /// the current process.
    /// </summary>
    public bool Durable { get; init; }
}
