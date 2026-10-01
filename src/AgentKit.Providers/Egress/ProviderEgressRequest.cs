// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Egress;

using System.Net.Http;

/// <summary>One fully prepared provider wire request together with the identity the egress grant binds.</summary>
/// <remarks>
/// <para>
/// The request captures the exact descriptor, endpoint/credential profile binding, model, attempt, and deadline of one
/// provider attempt so the egress grant is bound to them rather than to registration order. The
/// <see cref="Message"/> is an in-memory container for the method, absolute URI, headers, and body; no HTTP client or
/// handler ever sends it. <see cref="ProviderEgress"/> reads it once, freezes the body bytes, and sends those bytes
/// through <see cref="INetworkTransport"/>.
/// </para>
/// <para>
/// The caller keeps ownership of <see cref="Message"/> and disposes it after the send returns. Instances are
/// immutable apart from that message and are safe to build per attempt.
/// </para>
/// </remarks>
public sealed record ProviderEgressRequest
{
    /// <summary>Initializes a new instance of the <see cref="ProviderEgressRequest"/> record.</summary>
    /// <param name="operation">
    /// The protected semantic operation whose captured authorization selects the security authority, or
    /// <see langword="null"/> when the caller has none; a missing operation refuses before any I/O.
    /// </param>
    /// <param name="kind">The independent provider operation being performed.</param>
    /// <param name="providerId">The branded provider identity.</param>
    /// <param name="apiFamily">The wire API family.</param>
    /// <param name="serviceSurface">The provider service surface this operation uses.</param>
    /// <param name="endpointId">The provider endpoint identity.</param>
    /// <param name="binding">The exact endpoint and credential profile binding, or <see langword="null"/> for an unbound operation.</param>
    /// <param name="modelId">The provider model identity.</param>
    /// <param name="deploymentId">The optional deployment identity.</param>
    /// <param name="modelRevision">The optional descriptor or model revision the grant binds.</param>
    /// <param name="attempt">The one-based attempt number.</param>
    /// <param name="deadline">The absolute attempt deadline.</param>
    /// <param name="streaming">Whether the response is consumed as a stream.</param>
    /// <param name="message">The prepared method, URI, headers, and body.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined or <paramref name="attempt"/> is below one.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> has no absolute <see cref="HttpRequestMessage.RequestUri"/>.
    /// </exception>
    public ProviderEgressRequest(
        ProtectedSemanticOperationContext? operation,
        ProviderEgressOperation kind,
        ProviderId providerId,
        ApiFamilyId apiFamily,
        ProviderServiceSurfaceId serviceSurface,
        ProviderEndpointId endpointId,
        ProviderOperationBinding? binding,
        ModelId modelId,
        DeploymentId? deploymentId,
        string? modelRevision,
        int attempt,
        DateTimeOffset deadline,
        bool streaming,
        HttpRequestMessage message)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentNullException.ThrowIfNull(message);
        if (message.RequestUri is not { IsAbsoluteUri: true })
        {
            throw new ArgumentException("The provider message requires an absolute request URI.", nameof(message));
        }

        Operation = operation;
        Kind = kind;
        ProviderId = providerId;
        ApiFamily = apiFamily;
        ServiceSurface = serviceSurface;
        EndpointId = endpointId;
        Binding = binding;
        ModelId = modelId;
        DeploymentId = deploymentId;
        ModelRevision = modelRevision;
        Attempt = attempt;
        Deadline = deadline;
        Streaming = streaming;
        Message = message;
    }

    /// <summary>Gets the protected semantic operation that selects the security authority.</summary>
    /// <value>The operation, or <see langword="null"/> when none was supplied.</value>
    public ProtectedSemanticOperationContext? Operation { get; init; }

    /// <summary>Gets the independent provider operation being performed.</summary>
    public ProviderEgressOperation Kind { get; init; }

    /// <summary>Gets the branded provider identity.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>Gets the wire API family.</summary>
    public ApiFamilyId ApiFamily { get; init; }

    /// <summary>Gets the provider service surface.</summary>
    public ProviderServiceSurfaceId ServiceSurface { get; init; }

    /// <summary>Gets the provider endpoint identity.</summary>
    public ProviderEndpointId EndpointId { get; init; }

    /// <summary>Gets the exact endpoint and credential profile binding.</summary>
    /// <value>The binding, or <see langword="null"/> when the operation is not bound to versioned profiles.</value>
    public ProviderOperationBinding? Binding { get; init; }

    /// <summary>Gets the provider model identity.</summary>
    public ModelId ModelId { get; init; }

    /// <summary>Gets the optional deployment identity.</summary>
    public DeploymentId? DeploymentId { get; init; }

    /// <summary>Gets the optional descriptor or model revision the grant binds.</summary>
    public string? ModelRevision { get; init; }

    /// <summary>Gets the one-based attempt number.</summary>
    public int Attempt { get; init; }

    /// <summary>Gets the absolute attempt deadline.</summary>
    public DateTimeOffset Deadline { get; init; }

    /// <summary>Gets whether the response is consumed as a stream.</summary>
    public bool Streaming { get; init; }

    /// <summary>Gets the prepared method, URI, headers, and body; the caller retains ownership.</summary>
    public HttpRequestMessage Message { get; init; }

    /// <summary>Creates the egress identity for one conversational attempt.</summary>
    /// <param name="descriptor">The exact descriptor the adapter serves.</param>
    /// <param name="request">The attempt request carrying the operation, attempt number, and deadline.</param>
    /// <param name="message">The prepared wire message.</param>
    /// <param name="streaming">Whether the response is consumed as a stream.</param>
    /// <returns>The request binding the egress grant to <paramref name="descriptor"/> and its profile binding.</returns>
    /// <exception cref="ArgumentNullException">Any reference argument is null.</exception>
    public static ProviderEgressRequest ForConversation(
        ModelDescriptor descriptor,
        LlmModelRequest request,
        HttpRequestMessage message,
        bool streaming)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(message);
        return new ProviderEgressRequest(
            request.Operation,
            ProviderEgressOperation.Conversation,
            descriptor.ProviderId,
            descriptor.ApiFamily,
            descriptor.ServiceSurface,
            descriptor.EndpointId,
            descriptor.Binding,
            descriptor.ModelId,
            descriptor.DeploymentId,
            descriptor.DescriptorRevision.ToString(),
            request.Attempt,
            request.Deadline,
            streaming,
            message);
    }

    /// <summary>Creates the egress identity for one embedding attempt.</summary>
    /// <param name="descriptor">The exact descriptor the adapter serves.</param>
    /// <param name="request">The attempt request carrying the operation, attempt number, and deadline.</param>
    /// <param name="message">The prepared wire message.</param>
    /// <returns>The request binding the egress grant to <paramref name="descriptor"/> and its profile binding.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static ProviderEgressRequest ForEmbedding(
        EmbeddingModelDescriptor descriptor,
        EmbeddingModelRequest request,
        HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(message);
        return new ProviderEgressRequest(
            request.Operation,
            ProviderEgressOperation.Embedding,
            descriptor.ProviderId,
            descriptor.ApiFamily,
            descriptor.ServiceSurface,
            descriptor.EndpointId,
            descriptor.Binding,
            descriptor.ModelId,
            descriptor.DeploymentId,
            descriptor.ModelRevision?.Value,
            request.Attempt,
            request.Deadline,
            streaming: false,
            message);
    }

    /// <summary>Creates the egress identity for one reranking attempt.</summary>
    /// <param name="descriptor">The exact descriptor the adapter serves.</param>
    /// <param name="request">The attempt request carrying the operation, attempt number, and deadline.</param>
    /// <param name="message">The prepared wire message.</param>
    /// <returns>The request binding the egress grant to <paramref name="descriptor"/> and its profile binding.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static ProviderEgressRequest ForReranking(
        RerankerDescriptor descriptor,
        RerankModelRequest request,
        HttpRequestMessage message)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(message);
        return new ProviderEgressRequest(
            request.Operation,
            ProviderEgressOperation.Reranking,
            descriptor.ProviderId,
            descriptor.ApiFamily,
            descriptor.ServiceSurface,
            descriptor.EndpointId,
            descriptor.Binding,
            descriptor.ModelId,
            descriptor.DeploymentId,
            descriptor.ModelRevision?.Value,
            request.Attempt,
            request.Deadline,
            streaming: false,
            message);
    }
}
