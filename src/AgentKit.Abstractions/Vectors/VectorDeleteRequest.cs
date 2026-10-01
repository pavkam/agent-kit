// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Asks a vector index to remove vectors by chunk identity, propagating a document deletion or version switch.</summary>
/// <remarks>Deleting an absent chunk is not an error, so a receipt's chunk list can be replayed safely. The request names the complete vector space, which the index checks before it mutates state.</remarks>
public sealed record VectorDeleteRequest
{
    /// <summary>Initializes a validated delete request.</summary>
    /// <param name="space">The complete vector space of the index.</param>
    /// <param name="chunkIds">The non-empty chunk identities to remove, at most <see cref="VectorUpsertRequest.MaximumBatch"/>.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <param name="grant">The single-use grant for this exact operation.</param>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException">The list is default, empty, or contains a default identity; the key is blank; or the grant lacks captured authorization.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The list exceeds the batch bound.</exception>
    public VectorDeleteRequest(VectorSpaceDescriptor space, ImmutableArray<ChunkId> chunkIds, IdempotencyKey idempotencyKey, SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentException.ThrowIfDefaultOrEmpty(chunkIds, nameof(chunkIds));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunkIds.Length, VectorUpsertRequest.MaximumBatch, nameof(chunkIds));
        foreach (var chunk in chunkIds)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(chunk, default, nameof(chunkIds));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(grant.Authorization, nameof(grant));
        Space = space;
        ChunkIds = chunkIds;
        IdempotencyKey = idempotencyKey;
        Grant = grant;
    }

    /// <summary>Gets the complete vector space of the index.</summary>
    public VectorSpaceDescriptor Space { get; }

    /// <summary>Gets the chunk identities to remove.</summary>
    public ImmutableArray<ChunkId> ChunkIds { get; }

    /// <summary>Gets the replay key.</summary>
    public IdempotencyKey IdempotencyKey { get; }

    /// <summary>Gets the single-use grant for this exact operation.</summary>
    public SecurityGrant Grant { get; }
}
