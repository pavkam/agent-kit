// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Produces the protected resources and canonical fingerprints that bind a security grant to one exact vector-index operation.</summary>
/// <remarks>
/// An index derives the same resource and fingerprint from the request it is executing and asks the grant store to consume a
/// grant that binds them. Searches use <see cref="SecurityOperationKind.StateRead"/> and writes use
/// <see cref="SecurityOperationKind.StateMutation"/>. Payloads are built from identities, hashes, and numbers only.
/// </remarks>
public static class VectorSecurityBinding
{
    /// <summary>Names one vector index as a protected resource.</summary>
    /// <param name="key">The index key.</param>
    /// <returns>The protected index resource.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
    public static ProtectedResource Resource(VectorIndexKey key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return new(ProtectedResourceKind.ApplicationState, $"vector-index:{key.Value}");
    }

    /// <summary>Fingerprints a batch upsert.</summary>
    /// <param name="space">The vector space of the batch.</param>
    /// <param name="records">The batch.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <returns>A deterministic fingerprint of the space, chunk identities, and source hashes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="records"/> is default or contains null, or the key is blank.</exception>
    public static InputFingerprint UpsertFingerprint(VectorSpaceDescriptor space, ImmutableArray<VectorRecord> records, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentException.ThrowIfDefault(records);
        ArgumentException.ThrowIfContainsNull(records);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new UpsertPayload(
            space.IndexKey.Value, space.Dimensions, space.DistanceMetric,
            [.. records.Select(static record => $"{record.ChunkId.Value:N}:{record.SourceHash.Value}:{record.DocumentVersion.Value}")],
            idempotencyKey.Value));
    }

    /// <summary>Fingerprints a search.</summary>
    /// <param name="space">The vector space of the query.</param>
    /// <param name="query">The query vector.</param>
    /// <param name="topK">The number of matches requested.</param>
    /// <param name="documents">The document restriction, or default for none.</param>
    /// <returns>A deterministic fingerprint of the space, query, bound, and restriction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="query"/> is default.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="topK"/> is not positive.</exception>
    public static InputFingerprint SearchFingerprint(VectorSpaceDescriptor space, ImmutableArray<float> query, int topK, ImmutableArray<DocumentId> documents)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentException.ThrowIfDefault(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(topK);
        return SecurityCanonicalFingerprint.Create(new SearchPayload(
            space.IndexKey.Value, space.Dimensions, space.DistanceMetric, query, topK,
            [.. (documents.IsDefault ? [] : documents).Select(static document => document.Value).Order()]));
    }

    /// <summary>Fingerprints a batch deletion.</summary>
    /// <param name="space">The vector space of the index.</param>
    /// <param name="chunkIds">The chunks to remove.</param>
    /// <param name="idempotencyKey">The replay key.</param>
    /// <returns>A deterministic fingerprint of the space and chunk identities.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="chunkIds"/> is default or the key is blank.</exception>
    public static InputFingerprint DeleteFingerprint(VectorSpaceDescriptor space, ImmutableArray<ChunkId> chunkIds, IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentException.ThrowIfDefault(chunkIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey.Value, nameof(idempotencyKey));
        return SecurityCanonicalFingerprint.Create(new DeletePayload(
            space.IndexKey.Value, space.Dimensions, space.DistanceMetric, [.. chunkIds.Select(static chunk => chunk.Value)], idempotencyKey.Value));
    }

    private sealed record UpsertPayload(string IndexKey, int Dimensions, VectorDistanceMetric Metric, ImmutableArray<string> Records, string IdempotencyKey);

    private sealed record SearchPayload(string IndexKey, int Dimensions, VectorDistanceMetric Metric, ImmutableArray<float> Query, int TopK, ImmutableArray<Guid> Documents);

    private sealed record DeletePayload(string IndexKey, int Dimensions, VectorDistanceMetric Metric, ImmutableArray<Guid> ChunkIds, string IdempotencyKey);
}
