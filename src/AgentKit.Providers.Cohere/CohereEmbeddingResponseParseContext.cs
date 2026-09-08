// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Cohere;

/// <summary>
/// The correlation and identity context an
/// <see cref="ICohereEmbeddingResponseParser"/> needs to translate one raw
/// Cohere v2 embed HTTP response body into a terminal
/// <see cref="EmbeddingAttemptResult"/>.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record CohereEmbeddingResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="CohereEmbeddingResponseParseContext"/> record.</summary>
    /// <param name="requestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="requestedEncoding">
    /// The single encoding requested for returned vectors, used both to
    /// select the <c>embeddings</c> map key to read and to decode each
    /// raw vector into the matching <see cref="EmbeddingVector"/> kind.
    /// </param>
    /// <param name="requestedPurpose">
    /// The purpose the request declared, echoed onto each resulting
    /// <see cref="EmbeddingSpaceIdentity"/> because Cohere's asymmetric
    /// query/document embedding spaces are not comparable across purposes.
    /// </param>
    public CohereEmbeddingResponseParseContext(
        EmbeddingRequestId requestId,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        EmbeddingEncoding requestedEncoding,
        EmbeddingPurpose requestedPurpose)
    {
        RequestId = requestId;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        RequestedModelId = requestedModelId;
        RequestedEncoding = requestedEncoding;
        RequestedPurpose = requestedPurpose;
    }

    /// <summary>Gets the identity of the request this response answers.</summary>
    public EmbeddingRequestId RequestId { get; init; }

    /// <summary>Gets the provider that produced the response.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>Gets the wire/API family used for the request.</summary>
    public ApiFamilyId ApiFamily { get; init; }

    /// <summary>Gets the model identity that was requested.</summary>
    public ModelId RequestedModelId { get; init; }

    /// <summary>
    /// Gets the single encoding requested for returned vectors, used both
    /// to select the <c>embeddings</c> map key to read and to decode each
    /// raw vector into the matching <see cref="EmbeddingVector"/> kind.
    /// </summary>
    public EmbeddingEncoding RequestedEncoding { get; init; }

    /// <summary>
    /// Gets the purpose the request declared, echoed onto each resulting
    /// <see cref="EmbeddingSpaceIdentity"/> because Cohere's asymmetric
    /// query/document embedding spaces are not comparable across
    /// purposes.
    /// </summary>
    public EmbeddingPurpose RequestedPurpose { get; init; }
}
