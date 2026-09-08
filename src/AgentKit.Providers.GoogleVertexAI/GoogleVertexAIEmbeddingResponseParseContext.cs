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
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record GoogleVertexAIEmbeddingResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="GoogleVertexAIEmbeddingResponseParseContext"/> record.</summary>
    /// <param name="requestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="deploymentId">The concrete deployment or endpoint used, when applicable.</param>
    public GoogleVertexAIEmbeddingResponseParseContext(
        EmbeddingRequestId requestId,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        DeploymentId? deploymentId)
    {
        RequestId = requestId;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        RequestedModelId = requestedModelId;
        DeploymentId = deploymentId;
    }

    /// <summary>Gets the identity of the request this response answers.</summary>
    public EmbeddingRequestId RequestId { get; init; }

    /// <summary>Gets the provider that produced the response.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>Gets the wire/API family used for the request.</summary>
    public ApiFamilyId ApiFamily { get; init; }

    /// <summary>Gets the model identity that was requested.</summary>
    public ModelId RequestedModelId { get; init; }

    /// <summary>Gets the concrete deployment or endpoint used, when applicable.</summary>
    public DeploymentId? DeploymentId { get; init; }
}
