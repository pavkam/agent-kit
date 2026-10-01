// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Searches and maintains vectors of exactly one compatible vector space behind an exact security boundary.</summary>
/// <remarks>
/// <para>
/// An index holds one space and rejects any request whose complete <see cref="VectorSpaceDescriptor"/> differs from
/// <see cref="VectorSpace"/> before it touches state; a matching dimension alone is not compatibility. Every operation
/// consumes a single-use <see cref="SecurityGrant"/> that binds that exact operation, and search is confined to the authorized
/// tenant, agent, and principal visibility.
/// </para>
/// <para>
/// Implementations are thread-safe. An index is derived state: its hits are pointers that callers filter against the
/// authoritative active document version. Re-embedding is a migration to a new index followed by an atomic pointer switch.
/// </para>
/// </remarks>
public interface IVectorIndex
{
    /// <summary>Gets the single vector space the index holds.</summary>
    /// <value>An immutable descriptor that does not change after construction.</value>
    public VectorSpaceDescriptor VectorSpace { get; }

    /// <summary>Gets the security audience grants for this index must name.</summary>
    /// <value>A non-blank component identity.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Gets a value indicating whether an acknowledged write survives process loss.</summary>
    public bool IsDurable { get; }

    /// <summary>Gets a value indicating whether search is approximate.</summary>
    /// <value><see langword="false"/> for an exact scan; adapters never claim approximate search without a verified index implementation.</value>
    public bool ApproximateSearch { get; }

    /// <summary>Stores or replaces a batch of vectors atomically.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The count stored and the watermark, or a typed refusal; a descriptor mismatch is refused before any state is touched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<VectorUpsertResult> UpsertAsync(VectorUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>Returns the nearest vectors to a query inside the authorized scope.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the search completes.</param>
    /// <returns>Ranked matches with the watermark, or a typed refusal; a descriptor mismatch is refused before any search.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled.</exception>
    public ValueTask<VectorSearchResult> SearchAsync(VectorSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>Removes vectors by chunk identity atomically.</summary>
    /// <param name="request">The exact authorized request.</param>
    /// <param name="cancellationToken">Cancels before the write commits.</param>
    /// <returns>The count removed and the watermark, or a typed refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before the write commits.</exception>
    public ValueTask<VectorDeleteResult> DeleteAsync(VectorDeleteRequest request, CancellationToken cancellationToken = default);
}
