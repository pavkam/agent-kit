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
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record MistralAIEmbeddingResponseParseContext
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
    {
        RequestId = requestId;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        RequestedModelId = requestedModelId;
        RequestedEncoding = requestedEncoding;
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
    /// Gets the encoding requested for returned vectors, used to select
    /// which <see cref="EmbeddingVector"/> kind the raw <c>embedding</c>
    /// array decodes into.
    /// </summary>
    public EmbeddingEncoding? RequestedEncoding { get; init; }
}
