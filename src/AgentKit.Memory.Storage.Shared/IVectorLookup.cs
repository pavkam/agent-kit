// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Gives the vector planner read access to stored vectors and receipts, however an adapter keeps them.</summary>
/// <remarks>The planner calls these members only while the adapter holds whatever gate or transaction makes the answers consistent with the write that follows.</remarks>
internal interface IVectorLookup
{
    /// <summary>Gets the index watermark, which advances with every mutation.</summary>
    /// <value>Zero for an empty, never-mutated index.</value>
    public long Watermark { get; }

    /// <summary>Finds an earlier batch receipt by idempotency key.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="key">The batch key.</param>
    /// <returns>The receipt, or <see langword="null"/> when the key is unused.</returns>
    public VectorReceipt? FindReceipt(TenantId tenant, string key);

    /// <summary>Finds one stored vector.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="chunkId">The chunk the vector derives from.</param>
    /// <returns>The vector, or <see langword="null"/> when none is stored.</returns>
    public VectorEntry? Find(TenantId tenant, ChunkId chunkId);

    /// <summary>Streams one agent's vectors in a tenant.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="agent">The owning agent.</param>
    /// <returns>A lazily evaluated stream of the agent's vectors in a stable order.</returns>
    public IEnumerable<VectorEntry> Scan(TenantId tenant, AgentId agent);
}
