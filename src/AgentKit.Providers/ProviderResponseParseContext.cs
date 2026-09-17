// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// The correlation and identity context a conversational response parser
/// needs to translate one raw provider HTTP response body into a
/// <see cref="ModelResponseEvent"/> sequence and a terminal
/// <see cref="ModelAttemptResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every first-party conversational adapter captures the same six facts
/// before it reads a response: the AgentKit request identity, the provider
/// and API family that answered, the model that was requested, the concrete
/// deployment when one exists, and the provider's own request correlation
/// identifier when the transport exposed one. Parsers combine them with the
/// model and response identifiers found in the body through
/// <see cref="CreateResponseIdentity"/> so every provider builds
/// <see cref="ProviderResponseIdentity"/> by one rule.
/// </para>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// </remarks>
public sealed record ProviderResponseParseContext
{
    /// <summary>Initializes a new instance of the <see cref="ProviderResponseParseContext"/> record.</summary>
    /// <param name="modelRequestId">The identity of the request this response answers.</param>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="deploymentId">The concrete deployment or endpoint used, when applicable.</param>
    /// <param name="providerRequestId">
    /// The provider-supplied request correlation identifier read from the
    /// HTTP response, when the provider supplied one.
    /// </param>
    public ProviderResponseParseContext(
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

    /// <summary>
    /// Builds the <see cref="ProviderResponseIdentity"/> of a response parsed
    /// under this context, combining the captured request facts with the
    /// identifiers the provider reported in the response body.
    /// </summary>
    /// <param name="resolvedModel">
    /// The model identifier the provider reported in the body, or
    /// <see langword="null"/> or empty when it reported none; the requested
    /// model is then taken as the resolved model.
    /// </param>
    /// <param name="responseId">
    /// The provider's response identifier from the body, or
    /// <see langword="null"/> or empty when it reported none.
    /// </param>
    /// <returns>
    /// An identity with no upstream provider, this context's provider, API
    /// family, requested model, deployment, and provider request identifier,
    /// the resolved model as described, and the response identifier when one
    /// was reported.
    /// </returns>
    public ProviderResponseIdentity CreateResponseIdentity(string? resolvedModel = null, string? responseId = null) =>
        new(
            ProviderId,
            upstreamProviderId: null,
            ApiFamily,
            RequestedModelId,
            // ModelId/ProviderResponseId reject a whitespace-only value; "reported none" must include a
            // provider that reported an all-whitespace string, not only a null or literally empty one.
            !string.IsNullOrWhiteSpace(resolvedModel) ? new ModelId(resolvedModel) : RequestedModelId,
            DeploymentId,
            ProviderRequestId,
            !string.IsNullOrWhiteSpace(responseId) ? new ProviderResponseId(responseId) : null);
}
