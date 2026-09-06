// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The exact provider, API family, model, deployment, and correlation
/// identity of one provider response. Results, telemetry, and durable state
/// preserve this identity instead of the application-facing alias used to
/// select it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// Application configuration selects a model by a friendly alias (for
/// example, "fast-chat-model"), but that alias can resolve to different
/// concrete providers, models, or deployments over time, and a routing
/// provider can serve a request through an upstream provider different from
/// the one configured. <see cref="ProviderResponseIdentity"/> captures the
/// ground truth of what actually served one specific response — the real
/// <see cref="ProviderId"/> (and <see cref="UpstreamProviderId"/> if
/// routed), the wire <see cref="ApiFamily"/>, both the requested and
/// resolved model, and the concrete deployment — so historical records
/// remain accurate even after configuration changes, and so cost/usage
/// analysis is never misattributed to the alias instead of the provider
/// that actually did the work.
/// </para>
/// </remarks>
public sealed record ProviderResponseIdentity
{
    /// <summary>Initializes a new instance of the <see cref="ProviderResponseIdentity"/> record.</summary>
    /// <param name="providerId">The provider that produced the response.</param>
    /// <param name="upstreamProviderId">
    /// The upstream provider the request was routed to, when the configured
    /// provider is a router or broker; <see langword="null"/> when the
    /// configured provider served the request directly.
    /// </param>
    /// <param name="apiFamily">The wire/API family used for the request.</param>
    /// <param name="requestedModelId">The model identity that was requested.</param>
    /// <param name="resolvedModelId">
    /// The model identity that actually served the request. This can differ
    /// from <paramref name="requestedModelId"/> when the requested identity
    /// is itself a provider-side alias, such as a "latest" pointer.
    /// </param>
    /// <param name="deploymentId">The concrete deployment or endpoint used, when applicable.</param>
    /// <param name="requestId">The provider-supplied request correlation identifier, when available.</param>
    /// <param name="responseId">The provider-supplied response correlation identifier, when available.</param>
    public ProviderResponseIdentity(
        ProviderId providerId,
        ProviderId? upstreamProviderId,
        ApiFamilyId apiFamily,
        ModelId requestedModelId,
        ModelId resolvedModelId,
        DeploymentId? deploymentId,
        ProviderRequestId? requestId,
        ProviderResponseId? responseId)
    {
        ProviderId = providerId;
        UpstreamProviderId = upstreamProviderId;
        ApiFamily = apiFamily;
        RequestedModelId = requestedModelId;
        ResolvedModelId = resolvedModelId;
        DeploymentId = deploymentId;
        RequestId = requestId;
        ResponseId = responseId;
    }

    /// <summary>Gets the provider that produced the response.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>
    /// Gets the upstream provider the request was routed to, when the
    /// configured provider is a router or broker.
    /// </summary>
    public ProviderId? UpstreamProviderId { get; init; }

    /// <summary>Gets the wire/API family used for the request.</summary>
    public ApiFamilyId ApiFamily { get; init; }

    /// <summary>Gets the model identity that was requested.</summary>
    public ModelId RequestedModelId { get; init; }

    /// <summary>
    /// Gets the model identity that actually served the request, which can
    /// differ from <see cref="RequestedModelId"/> when the requested
    /// identity is itself a provider-side alias.
    /// </summary>
    public ModelId ResolvedModelId { get; init; }

    /// <summary>Gets the concrete deployment or endpoint used, when applicable.</summary>
    public DeploymentId? DeploymentId { get; init; }

    /// <summary>
    /// Gets the provider-supplied request correlation identifier, when
    /// available.
    /// </summary>
    public ProviderRequestId? RequestId { get; init; }

    /// <summary>
    /// Gets the provider-supplied response correlation identifier, when
    /// available.
    /// </summary>
    public ProviderResponseId? ResponseId { get; init; }
}
