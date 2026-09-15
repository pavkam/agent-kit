// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// The correlation and identity context an embedding response parser needs
/// to translate one raw provider HTTP response body into a terminal
/// <see cref="EmbeddingAttemptResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the embedding counterpart of
/// <see cref="ProviderResponseParseContext"/>: the same provider, API
/// family, requested model, deployment, and provider request identifier,
/// keyed by an <see cref="EmbeddingRequestId"/>. Providers whose embedding
/// endpoints expose no deployment or request correlation pass
/// <see langword="null"/> for those members. A provider whose parser needs
/// additional request-shape evidence (for example the encoding it asked the
/// service to return) derives its own record from this one so the shared
/// identity rule in <see cref="CreateResponseIdentity"/> is not duplicated.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// </remarks>
public record EmbeddingResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="EmbeddingResponseParseContext"/> record.</summary>
    /// <param name="requestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="deploymentId">The concrete deployment or endpoint used, or <see langword="null"/> when the provider has no deployment concept.</param>
    /// <param name="providerRequestId">
    /// The provider-supplied request correlation identifier read from the
    /// HTTP response, or <see langword="null"/> when the provider supplied
    /// none or the adapter does not read one for embeddings.
    /// </param>
    public EmbeddingResponseParseContext(
        EmbeddingRequestId requestId,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        DeploymentId? deploymentId,
        ProviderRequestId? providerRequestId)
    {
        RequestId = requestId;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        RequestedModelId = requestedModelId;
        DeploymentId = deploymentId;
        ProviderRequestId = providerRequestId;
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

    /// <summary>
    /// Gets the provider-supplied request correlation identifier read from
    /// the HTTP response, when the provider supplied one.
    /// </summary>
    public ProviderRequestId? ProviderRequestId { get; init; }

    /// <summary>
    /// Builds the <see cref="ProviderResponseIdentity"/> of an embedding
    /// response parsed under this context.
    /// </summary>
    /// <param name="resolvedModel">
    /// The model identifier the provider reported in the body, or
    /// <see langword="null"/> or empty when it reported none; the requested
    /// model is then taken as the resolved model.
    /// </param>
    /// <returns>
    /// An identity with no upstream provider, this context's provider, API
    /// family, requested model, and deployment, and the resolved model as
    /// described. Embedding responses carry no response identifier, and the
    /// provider request identifier is reported on
    /// <see cref="EmbeddingResponse.ProviderRequestId"/> rather than on the
    /// identity, so both identity members are <see langword="null"/>.
    /// </returns>
    public ProviderResponseIdentity CreateResponseIdentity(string? resolvedModel = null) =>
        new(
            ProviderId,
            upstreamProviderId: null,
            ApiFamily,
            RequestedModelId,
            resolvedModel is { Length: > 0 } model ? new ModelId(model) : RequestedModelId,
            DeploymentId,
            requestId: null,
            responseId: null);
}
