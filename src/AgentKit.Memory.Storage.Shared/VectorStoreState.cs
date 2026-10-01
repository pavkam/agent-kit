// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Holds tenant-partitioned vectors, batch receipts, and the watermark for adapters that keep state in process memory.</summary>
/// <remarks>
/// The class is not thread-safe: the owning adapter serializes every call under its own gate. The receipt window is bounded so a
/// long-lived index cannot grow without limit; an idempotency key older than the window is treated as unused.
/// </remarks>
internal sealed class VectorStoreState: IVectorLookup
{
    /// <summary>The largest number of batch receipts remembered for idempotent replay.</summary>
    internal const int ReceiptWindow = 4_096;

    private readonly Dictionary<(TenantId Tenant, ChunkId Chunk), VectorEntry> _vectors = [];
    private readonly Dictionary<(TenantId Tenant, string Key), VectorReceipt> _receipts = [];
    private readonly Queue<(TenantId Tenant, string Key)> _order = new();

    /// <inheritdoc/>
    public long Watermark { get; private set; }

    /// <summary>Gets the number of stored vectors across every tenant.</summary>
    internal int Count => _vectors.Count;

    /// <inheritdoc/>
    public VectorReceipt? FindReceipt(TenantId tenant, string key) => _receipts.GetValueOrDefault((tenant, key));

    /// <inheritdoc/>
    public VectorEntry? Find(TenantId tenant, ChunkId chunkId) => _vectors.GetValueOrDefault((tenant, chunkId));

    /// <inheritdoc/>
    public IEnumerable<VectorEntry> Scan(TenantId tenant, AgentId agent) =>
        _vectors.Values.Where(entry => entry.Tenant == tenant && entry.Record.AgentId == agent);

    /// <summary>Lists every stored vector, for compaction.</summary>
    /// <returns>Every vector in a stable order.</returns>
    internal IReadOnlyList<VectorEntry> SnapshotVectors() => [.. _vectors.Values.OrderBy(static entry => entry.Record.ChunkId.Value)];

    /// <summary>Lists every remembered receipt in the order it was applied, for compaction.</summary>
    /// <returns>The receipts, oldest first.</returns>
    internal IReadOnlyList<VectorReceipt> SnapshotReceipts() => [.. _order.Select(pair => _receipts[pair])];

    /// <summary>Commits a planned change to memory after the adapter persisted it.</summary>
    /// <param name="upserts">The vectors to store or replace.</param>
    /// <param name="deletes">The chunks to remove.</param>
    /// <param name="receipt">The receipt to remember, or <see langword="null"/>.</param>
    /// <param name="watermark">The watermark after the change.</param>
    internal void Commit(ImmutableArray<VectorEntry> upserts, ImmutableArray<ChunkId> deletes, VectorReceipt? receipt, long watermark)
    {
        foreach (var entry in upserts)
        {
            _vectors[(entry.Tenant, entry.Record.ChunkId)] = entry;
        }

        if (receipt is not null)
        {
            foreach (var chunk in deletes)
            {
                _ = _vectors.Remove((receipt.Tenant, chunk));
            }

            Remember(receipt);
        }

        Watermark = Math.Max(Watermark, watermark);
    }

    /// <summary>Installs one vector while rebuilding memory from a durable log.</summary>
    /// <param name="entry">The vector to install.</param>
    internal void RestoreVector(VectorEntry entry) => _vectors[(entry.Tenant, entry.Record.ChunkId)] = entry;

    /// <summary>Removes one vector while rebuilding memory from a durable log.</summary>
    /// <param name="tenant">The tenant partition.</param>
    /// <param name="chunk">The chunk to remove.</param>
    internal void RestoreDelete(TenantId tenant, ChunkId chunk) => _ = _vectors.Remove((tenant, chunk));

    /// <summary>Installs one receipt while rebuilding memory from a durable log.</summary>
    /// <param name="receipt">The receipt to remember.</param>
    internal void RestoreReceipt(VectorReceipt receipt)
    {
        Remember(receipt);
        Watermark = Math.Max(Watermark, receipt.Watermark);
    }

    private void Remember(VectorReceipt receipt)
    {
        var key = (receipt.Tenant, receipt.Key);
        if (_receipts.TryAdd(key, receipt))
        {
            _order.Enqueue(key);
            while (_order.Count > ReceiptWindow)
            {
                _ = _receipts.Remove(_order.Dequeue());
            }
        }
        else
        {
            _receipts[key] = receipt;
        }
    }
}
