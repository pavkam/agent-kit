// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is one stored memory aggregate: the record, its creation snapshot, its transition receipts, and any deletion evidence.</summary>
/// <remarks>The entry is immutable and is the unit every adapter persists. After a purge, the body is replaced in both snapshots and the receipts are dropped, so no deleted content survives in any field.</remarks>
/// <param name="Tenant">The tenant partition.</param>
/// <param name="Sequence">The store-wide creation sequence.</param>
/// <param name="CreateKey">The creation idempotency key.</param>
/// <param name="Created">The record as first written, returned for an equivalent creation replay.</param>
/// <param name="Current">The record as it stands now.</param>
/// <param name="Receipts">The applied transitions in order.</param>
/// <param name="Deletion">The deletion evidence, or <see langword="null"/> while the record is live.</param>
internal sealed record MemoryEntry(
    TenantId Tenant,
    long Sequence,
    string CreateKey,
    DurableMemoryRecord Created,
    DurableMemoryRecord Current,
    ImmutableArray<MemoryTransitionReceipt> Receipts,
    MemoryDeletion? Deletion);
