// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The identity that determines whether two embedding vectors are
/// comparable. Two vectors drawn from incompatible spaces must never be
/// compared by distance or similarity, even when their lengths happen to
/// match.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// A memory or vector-storage component persists this identity alongside
/// every stored vector and rejects a query embedding computed under an
/// incompatible identity rather than silently comparing across spaces. At
/// minimum, two identities are compatible only when their resolved model,
/// dimensions, element type, and purpose all agree; a provider using an
/// asymmetric transform for query versus document embeddings produces
/// vectors from different effective spaces for each purpose even though
/// nothing else about the request changed.
/// </para>
/// </remarks>
public sealed record EmbeddingSpaceIdentity
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingSpaceIdentity"/> record.</summary>
    /// <param name="provider">The exact provider, model, and correlation identity that produced this vector.</param>
    /// <param name="dimensions">The number of dimensions in the vector.</param>
    /// <param name="elementType">The element representation of the vector.</param>
    /// <param name="purpose">The portable intended use the vector was computed for.</param>
    /// <param name="extensions">Provider-specific space-identity data.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="provider"/> or <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="dimensions"/> is less than one.</exception>
    public EmbeddingSpaceIdentity(
        ProviderResponseIdentity provider,
        int dimensions,
        EmbeddingElementType elementType,
        EmbeddingPurpose purpose,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        ArgumentNullException.ThrowIfNull(extensions);

        Provider = provider;
        Dimensions = dimensions;
        ElementType = elementType;
        Purpose = purpose;
        Extensions = extensions;
    }

    /// <summary>Gets the exact provider, model, and correlation identity that produced this vector.</summary>
    public ProviderResponseIdentity Provider { get; init; }

    /// <summary>Gets the number of dimensions in the vector.</summary>
    public int Dimensions { get; init; }

    /// <summary>Gets the element representation of the vector.</summary>
    public EmbeddingElementType ElementType { get; init; }

    /// <summary>Gets the portable intended use the vector was computed for.</summary>
    public EmbeddingPurpose Purpose { get; init; }

    /// <summary>Gets provider-specific space-identity data.</summary>
    public ExtensionData Extensions { get; init; }
}
