// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is one query embedding together with the exact embedding space that produced it.</summary>
/// <remarks>A source compares the space against each vector index before searching; a dimension match alone is never compatibility.</remarks>
public sealed record RetrievalQueryEmbedding
{
    /// <summary>Initializes a validated embedding.</summary>
    /// <param name="vector">The non-empty vector of finite components.</param>
    /// <param name="space">The exact embedding space of the vector; its dimensions must equal the vector length.</param>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> is null.</exception>
    /// <exception cref="ArgumentException">The vector is default, empty, non-finite, or of a different length than the space's dimensions.</exception>
    public RetrievalQueryEmbedding(ImmutableArray<float> vector, EmbeddingSpaceIdentity space)
    {
        ArgumentException.ThrowIfDefaultOrEmpty(vector, nameof(vector));
        ArgumentNullException.ThrowIfNull(space);
        if (vector.Length != space.Dimensions)
        {
            throw new ArgumentException("The vector must have the embedding space's dimensions.", nameof(vector));
        }

        foreach (var component in vector)
        {
            if (!float.IsFinite(component))
            {
                throw new ArgumentException("Vector components must be finite.", nameof(vector));
            }
        }

        Vector = vector;
        Space = space;
    }

    /// <summary>Gets the query vector.</summary>
    public ImmutableArray<float> Vector { get; }

    /// <summary>Gets the exact embedding space that produced the vector.</summary>
    public EmbeddingSpaceIdentity Space { get; }
}
