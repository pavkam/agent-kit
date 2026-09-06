// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

/// <summary>
/// The correlation and identity context an <see cref="IOpenAIStreamParser"/>
/// needs to translate one raw OpenAI-compatible HTTP response body into a
/// <see cref="ModelResponseEvent"/> sequence.
/// </summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </remarks>
public sealed record OpenAIResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="OpenAIResponseParseContext"/> record.</summary>
    /// <param name="modelRequestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="deploymentId">The concrete deployment or endpoint used, when applicable.</param>
    /// <param name="providerRequestId">
    /// The provider-supplied request correlation identifier read from the
    /// HTTP response, when the provider supplied one.
    /// </param>
    public OpenAIResponseParseContext(
        ModelRequestId modelRequestId,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        DeploymentId? deploymentId,
        ProviderRequestId? providerRequestId)
    {
        ModelRequestId = modelRequestId;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        RequestedModelId = requestedModelId;
        DeploymentId = deploymentId;
        ProviderRequestId = providerRequestId;
    }

    /// <summary>Gets the identity of the request this response answers.</summary>
    public ModelRequestId ModelRequestId { get; init; }

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
}
