// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Owns optimistic, idempotent, durable memory state behind an exact security boundary.</summary>
/// <remarks>
/// <para>
/// A store persists records, lifecycle transitions, and tombstones; it does not own policy, authorization decisions, ranking,
/// or events. Every operation consumes a single-use <see cref="SecurityGrant"/> that binds that exact operation before any
/// state is read or written, and resolves identities only inside the authorized tenant, agent, and principal visibility, so an
/// inaccessible item is reported as not found.
/// </para>
/// <para>
/// Implementations are thread-safe. Different records progress concurrently; one record's transitions are serialized by its
/// version token. Claims in <see cref="Descriptor"/> state the guarantees the adapter actually provides.
/// </para>
/// </remarks>
public interface IMemoryStore
{
    /// <summary>Gets the store's identity and capability claims.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public MemoryStoreDescriptor Descriptor { get; }

    /// <summary>Durably creates one record, or returns the original record for an equivalent replay.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The durable record or a typed refusal; grant denial happens before any write.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<MemoryWriteResult> WriteAsync(MemoryWriteRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one record by identity.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>The record, the tombstone of a deleted memory, or a typed refusal. An item outside the authorized scope is not found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken = default);

    /// <summary>Reads one page of the authorized agent's records in stable sequence order.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the read completes.</param>
    /// <returns>A page with a cursor and the deletion generation, or a typed refusal. Deleted records never appear.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<MemoryListResult> ListAsync(MemoryListRequest request, CancellationToken cancellationToken = default);

    /// <summary>Applies one lifecycle transition as a version compare-and-set, or returns the original result for an equivalent replay.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The record after the transition or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<MemoryTransitionResult> TransitionAsync(MemoryTransitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes one record by committing a tombstone and, when requested, physically purging its body.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>A receipt separating logical invisibility from physical purge, or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<MemoryDeleteResult> DeleteAsync(MemoryDeleteRequest request, CancellationToken cancellationToken = default);
}
