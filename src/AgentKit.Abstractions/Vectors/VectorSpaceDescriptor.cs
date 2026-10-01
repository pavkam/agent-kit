// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names one vector space: an index key, the exact embedding space of its vectors, and the distance metric that compares them.</summary>
/// <remarks>
/// A matching dimension count alone is never compatibility. The complete descriptor, including the provider identity, model
/// revision, element type, normalization, truncation, endpoint, and metric, must match before an index is contacted, so vectors
/// from different spaces are never mixed or compared. Per-response identifiers and the query-versus-document purpose do not
/// change a space; see <see cref="EmbeddingSpaceCompatibility"/>.
/// </remarks>
public sealed record VectorSpaceDescriptor
{
    /// <summary>Initializes a validated descriptor.</summary>
    /// <param name="indexKey">The key of the index that owns the space.</param>
    /// <param name="embeddingSpace">The exact embedding space of the stored vectors.</param>
    /// <param name="distanceMetric">The metric that compares vectors in the space.</param>
    /// <exception cref="ArgumentNullException"><paramref name="embeddingSpace"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="indexKey"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distanceMetric"/> is undefined.</exception>
    public VectorSpaceDescriptor(VectorIndexKey indexKey, EmbeddingSpaceIdentity embeddingSpace, VectorDistanceMetric distanceMetric)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(indexKey.Value, nameof(indexKey));
        ArgumentNullException.ThrowIfNull(embeddingSpace);
        ArgumentOutOfRangeException.ThrowIfUndefined(distanceMetric);
        IndexKey = indexKey;
        EmbeddingSpace = embeddingSpace;
        DistanceMetric = distanceMetric;
    }

    /// <summary>Gets the key of the index that owns the space.</summary>
    public VectorIndexKey IndexKey { get; }

    /// <summary>Gets the exact embedding space of the stored vectors.</summary>
    public EmbeddingSpaceIdentity EmbeddingSpace { get; }

    /// <summary>Gets the metric that compares vectors in the space.</summary>
    public VectorDistanceMetric DistanceMetric { get; }

    /// <summary>Gets the number of components every vector in the space has.</summary>
    public int Dimensions => EmbeddingSpace.Dimensions;

    /// <summary>Determines whether another descriptor names exactly the same vector space.</summary>
    /// <param name="other">The descriptor to compare.</param>
    /// <returns><see langword="true"/> only when the index key and metric match and the embedding spaces satisfy <see cref="EmbeddingSpaceCompatibility"/>; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is null.</exception>
    public bool IsCompatibleWith(VectorSpaceDescriptor other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return IndexKey == other.IndexKey && DistanceMetric == other.DistanceMetric && EmbeddingSpace.IsSameVectorSpaceAs(other.EmbeddingSpace);
    }
}
