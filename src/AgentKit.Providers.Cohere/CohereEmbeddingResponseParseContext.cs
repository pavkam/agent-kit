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
/// This record extends the shared <see cref="EmbeddingResponseParseContext"/>
/// with the encoding and purpose the request declared, which the parser
/// needs to select the <c>embeddings</c> map key and to stamp each vector's
/// embedding space. Cohere's embed endpoint exposes no deployment concept and
/// the adapter reads no request-correlation header for embeddings, so the
/// inherited <see cref="EmbeddingResponseParseContext.DeploymentId"/> and
/// <see cref="EmbeddingResponseParseContext.ProviderRequestId"/> are always
/// <see langword="null"/>. It remains an immutable value object with
/// structural equality over its fields and is safe to share across threads.
/// </remarks>
public sealed record CohereEmbeddingResponseParseContext: EmbeddingResponseParseContext
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
        : base(requestId, providerId, apiFamily, requestedModelId, deploymentId: null, providerRequestId: null)
    {
        RequestedEncoding = requestedEncoding;
        RequestedPurpose = requestedPurpose;
    }

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
