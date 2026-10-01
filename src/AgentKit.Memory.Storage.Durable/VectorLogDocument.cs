// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is one persisted log record of a durable vector index: a batch of changes with its receipt, or a compaction snapshot slice.</summary>
/// <param name="Tenant">The tenant partition text of every change in the record.</param>
/// <param name="Upserts">The vectors stored or replaced.</param>
/// <param name="Deletes">The chunks removed.</param>
/// <param name="Receipt">The batch receipt, or <see langword="null"/> for a snapshot slice of vectors.</param>
internal sealed record VectorLogDocument(
    string Tenant,
    ImmutableArray<VectorRecordDocument> Upserts,
    ImmutableArray<Guid> Deletes,
    VectorReceiptDocument? Receipt);
