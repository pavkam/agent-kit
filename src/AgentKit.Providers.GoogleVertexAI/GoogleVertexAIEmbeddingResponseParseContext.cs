// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleVertexAI;

/// <summary>
/// The correlation and identity context an
/// <see cref="IGoogleVertexAIEmbeddingResponseParser"/> needs to translate
/// one raw Vertex AI <c>:predict</c> HTTP response body into a terminal
/// <see cref="EmbeddingAttemptResult"/>.
/// </summary>
/// <remarks>
/// This record extends the shared <see cref="EmbeddingResponseParseContext"/>
/// with the purpose the request declared, which the parser needs to stamp
/// each vector's embedding space: Vertex uses asymmetric transforms per
/// <c>task_type</c> (<c>RETRIEVAL_QUERY</c>, <c>RETRIEVAL_DOCUMENT</c>,
/// <c>CLASSIFICATION</c>, ...), so vectors computed under different task
/// types are not comparable and must carry distinct
/// <see cref="EmbeddingSpaceIdentity"/> values. It remains an immutable
/// value object with structural equality over its fields and is safe to
/// share across threads.
/// </remarks>
public sealed record GoogleVertexAIEmbeddingResponseParseContext: EmbeddingResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="GoogleVertexAIEmbeddingResponseParseContext"/> record.</summary>
    /// <param name="requestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="deploymentId">The concrete deployment or endpoint used, or <see langword="null"/> when the provider has no deployment concept.</param>
    /// <param name="requestedPurpose">
    /// The purpose the request declared as <c>task_type</c>, echoed onto each
    /// resulting <see cref="EmbeddingSpaceIdentity"/> because Vertex's
    /// asymmetric query/document embedding spaces are not comparable across
    /// purposes.
    /// </param>
    /// <param name="providerRequestId">
    /// The provider-supplied request correlation identifier read from the
    /// HTTP response, or <see langword="null"/> when the provider supplied
    /// none.
    /// </param>
    public GoogleVertexAIEmbeddingResponseParseContext(
        EmbeddingRequestId requestId,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        DeploymentId? deploymentId,
        EmbeddingPurpose requestedPurpose,
        ProviderRequestId? providerRequestId = null)
        : base(requestId, providerId, apiFamily, requestedModelId, deploymentId, providerRequestId) =>
        RequestedPurpose = requestedPurpose;

    /// <summary>
    /// Gets the purpose the request declared as <c>task_type</c>, echoed
    /// onto each resulting <see cref="EmbeddingSpaceIdentity"/> because
    /// Vertex's asymmetric query/document embedding spaces are not
    /// comparable across purposes.
    /// </summary>
    public EmbeddingPurpose RequestedPurpose { get; init; }
}
