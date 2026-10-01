// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Is the outcome of planning one vector mutation: the typed result and the changes an adapter must persist for it.</summary>
/// <remarks>Adapters that persist must write the changes durably and only then commit them to memory. An empty plan means the result is a replay or refusal and nothing changes.</remarks>
/// <typeparam name="TResult">The typed operation result.</typeparam>
/// <param name="Result">The result to return to the caller.</param>
/// <param name="Upserts">The vectors to store or replace.</param>
/// <param name="Deletes">The chunks to remove from the tenant partition.</param>
/// <param name="Receipt">The receipt to remember, or <see langword="null"/> when nothing changes.</param>
/// <param name="Watermark">The index watermark after the change, meaningful only with a receipt.</param>
internal readonly record struct VectorPlan<TResult>(
    TResult Result,
    ImmutableArray<VectorEntry> Upserts,
    ImmutableArray<ChunkId> Deletes,
    VectorReceipt? Receipt,
    long Watermark);
