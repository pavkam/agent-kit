// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Gives the memory planner read access to stored entries, however an adapter keeps them.</summary>
/// <remarks>An in-memory adapter answers from dictionaries and a SQL adapter from queries inside its transaction. The planner calls these members only while the adapter holds whatever gate or transaction makes the answers consistent with the write that follows.</remarks>
internal interface IMemoryLookup
{
    /// <summary>Gets the creation sequence the next new entry receives.</summary>
    /// <value>One greater than the highest stored creation sequence.</value>
    public long NextSequence { get; }

    /// <summary>Gets the highest deletion generation assigned so far.</summary>
    /// <value>Zero before any deletion.</value>
    public long CurrentGeneration { get; }

    /// <summary>Finds an entry by its creation idempotency key.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="key">The creation key.</param>
    /// <returns>The entry, or <see langword="null"/> when the key is unused.</returns>
    public MemoryEntry? FindByCreateKey(TenantId tenant, string key);

    /// <summary>Finds an entry by identity.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="id">The memory identity.</param>
    /// <returns>The entry, or <see langword="null"/> when the tenant has none.</returns>
    public MemoryEntry? Find(TenantId tenant, MemoryId id);

    /// <summary>Streams one agent's entries with a sequence above a cursor, in ascending sequence order.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="agent">The owning agent.</param>
    /// <param name="afterSequence">The exclusive sequence cursor.</param>
    /// <returns>A lazily evaluated ordered stream the caller may stop early.</returns>
    public IEnumerable<MemoryEntry> Scan(TenantId tenant, AgentId agent, long afterSequence);
}
