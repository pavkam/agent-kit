// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.MistralAI;

/// <summary>
/// The correlation and identity context an
/// <see cref="IMistralAIEmbeddingResponseParser"/> needs to translate one
/// raw Mistral AI embeddings HTTP response body into a terminal
/// <see cref="EmbeddingAttemptResult"/>.
/// </summary>
/// <remarks>
/// This record extends the shared <see cref="EmbeddingResponseParseContext"/>
/// with the encoding the request asked for, which the parser needs to decode
/// the raw <c>embedding</c> array. Mistral's embeddings endpoint exposes no
/// deployment concept and the adapter reads no request-correlation header for
/// embeddings, so the inherited <see cref="EmbeddingResponseParseContext.DeploymentId"/>
/// and <see cref="EmbeddingResponseParseContext.ProviderRequestId"/> are
/// always <see langword="null"/>. It remains an immutable value object with
/// structural equality over its fields and is safe to share across threads.
/// </remarks>
public sealed record MistralAIEmbeddingResponseParseContext: EmbeddingResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="MistralAIEmbeddingResponseParseContext"/> record.</summary>
    /// <param name="requestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="requestedEncoding">
    /// The encoding requested for returned vectors, used to select which
    /// <see cref="EmbeddingVector"/> kind the raw <c>embedding</c> array
    /// decodes into.
    /// </param>
    public MistralAIEmbeddingResponseParseContext(
        EmbeddingRequestId requestId,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        EmbeddingEncoding? requestedEncoding)
        : base(requestId, providerId, apiFamily, requestedModelId, deploymentId: null, providerRequestId: null) =>
        RequestedEncoding = requestedEncoding;

    /// <summary>
    /// Gets the encoding requested for returned vectors, used to select
    /// which <see cref="EmbeddingVector"/> kind the raw <c>embedding</c>
    /// array decodes into.
    /// </summary>
    public EmbeddingEncoding? RequestedEncoding { get; init; }
}
