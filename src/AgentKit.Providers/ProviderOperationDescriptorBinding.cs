// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// Applies captured operation bindings to model descriptors published alongside leaf adapters.
/// </summary>
public static class ProviderOperationDescriptorBinding
{
    private static readonly ProviderEndpointProfileVersion _defaultEndpointVersion = new(1);
    private static readonly ProviderCredentialProfileVersion _defaultCredentialVersion = new(1);

    /// <summary>
    /// Returns a conversational descriptor stamped with the given service surface, endpoint identity, and binding.
    /// </summary>
    /// <param name="descriptor">The descriptor to augment.</param>
    /// <param name="serviceSurface">The service surface identity.</param>
    /// <param name="endpointProfileKey">The endpoint profile key referenced by the binding.</param>
    /// <param name="credentialProfileKey">The credential profile key referenced by the binding.</param>
    /// <param name="endpointId">The endpoint identity stamped on the descriptor.</param>
    /// <returns>The descriptor with binding metadata applied.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ModelDescriptor ApplyChatBinding(
        ModelDescriptor descriptor,
        ProviderServiceSurfaceId serviceSurface,
        ProviderEndpointProfileKey endpointProfileKey,
        ProviderCredentialProfileKey credentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with
        {
            ServiceSurface = serviceSurface,
            EndpointId = endpointId,
            Binding = CreateBinding(endpointProfileKey, credentialProfileKey),
        };
    }

    /// <summary>
    /// Returns an embedding descriptor stamped with the given service surface, endpoint identity, and binding.
    /// </summary>
    /// <param name="descriptor">The descriptor to augment.</param>
    /// <param name="serviceSurface">The service surface identity.</param>
    /// <param name="endpointProfileKey">The endpoint profile key referenced by the binding.</param>
    /// <param name="credentialProfileKey">The credential profile key referenced by the binding.</param>
    /// <param name="endpointId">The endpoint identity stamped on the descriptor.</param>
    /// <returns>The descriptor with binding metadata applied.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static EmbeddingModelDescriptor ApplyEmbeddingBinding(
        EmbeddingModelDescriptor descriptor,
        ProviderServiceSurfaceId serviceSurface,
        ProviderEndpointProfileKey endpointProfileKey,
        ProviderCredentialProfileKey credentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with
        {
            ServiceSurface = serviceSurface,
            EndpointId = endpointId,
            Binding = CreateBinding(endpointProfileKey, credentialProfileKey),
        };
    }

    /// <summary>
    /// Returns a reranker descriptor stamped with the given service surface, endpoint identity, and binding.
    /// </summary>
    /// <param name="descriptor">The descriptor to augment.</param>
    /// <param name="serviceSurface">The service surface identity.</param>
    /// <param name="endpointProfileKey">The endpoint profile key referenced by the binding.</param>
    /// <param name="credentialProfileKey">The credential profile key referenced by the binding.</param>
    /// <param name="endpointId">The endpoint identity stamped on the descriptor.</param>
    /// <returns>The descriptor with binding metadata applied.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static RerankerDescriptor ApplyRerankBinding(
        RerankerDescriptor descriptor,
        ProviderServiceSurfaceId serviceSurface,
        ProviderEndpointProfileKey endpointProfileKey,
        ProviderCredentialProfileKey credentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor with
        {
            ServiceSurface = serviceSurface,
            EndpointId = endpointId,
            Binding = CreateBinding(endpointProfileKey, credentialProfileKey),
        };
    }

    /// <summary>Creates one operation binding from stable profile keys at version one.</summary>
    /// <param name="endpointProfileKey">The endpoint profile key.</param>
    /// <param name="credentialProfileKey">The credential profile key.</param>
    /// <returns>The captured binding.</returns>
    public static ProviderOperationBinding CreateBinding(
        ProviderEndpointProfileKey endpointProfileKey,
        ProviderCredentialProfileKey credentialProfileKey) =>
        new(
            new ProviderEndpointProfileReference(endpointProfileKey, _defaultEndpointVersion),
            new ProviderCredentialProfileReference(credentialProfileKey, _defaultCredentialVersion));
}
