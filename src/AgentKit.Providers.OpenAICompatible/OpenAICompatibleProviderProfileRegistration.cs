// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.OpenAICompatible;

using AgentKit.Providers;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Shared profile and credential registration helpers for OpenAI-compatible provider leaves.</summary>
public static class OpenAICompatibleProviderProfileRegistration
{
    /// <summary>Registers default chat endpoint and credential profiles.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="chatServiceSurface">The chat service surface.</param>
    /// <param name="baseAddress">The chat endpoint base address.</param>
    /// <param name="credentialSourceKey">The credential source key.</param>
    /// <param name="chatEndpointProfileKey">The chat endpoint profile key.</param>
    /// <param name="chatCredentialProfileKey">The chat credential profile key.</param>
    /// <param name="endpointId">The endpoint identity stamped on descriptors.</param>
    /// <returns>The captured chat operation binding.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ProviderOperationBinding RegisterChatProfiles(
        IServiceCollection services,
        ProviderId providerId,
        ProviderServiceSurfaceId chatServiceSurface,
        Uri baseAddress,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderEndpointProfileKey chatEndpointProfileKey,
        ProviderCredentialProfileKey chatCredentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return ProviderOperationProfileRegistration.RegisterDefaultOperationProfiles(
            services,
            providerId,
            chatServiceSurface,
            baseAddress,
            credentialSourceKey,
            chatEndpointProfileKey,
            chatCredentialProfileKey,
            endpointId);
    }

    /// <summary>Registers default chat endpoint and credential profiles from a runtime base address.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="chatServiceSurface">The chat service surface.</param>
    /// <param name="baseAddressFactory">Resolves the chat endpoint base address at composition time.</param>
    /// <param name="credentialSourceKey">The credential source key.</param>
    /// <param name="chatEndpointProfileKey">The chat endpoint profile key.</param>
    /// <param name="chatCredentialProfileKey">The chat credential profile key.</param>
    /// <param name="endpointId">The endpoint identity stamped on descriptors.</param>
    /// <returns>The captured chat operation binding.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ProviderOperationBinding RegisterChatProfilesFromServices(
        IServiceCollection services,
        ProviderId providerId,
        ProviderServiceSurfaceId chatServiceSurface,
        Func<IServiceProvider, Uri> baseAddressFactory,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderEndpointProfileKey chatEndpointProfileKey,
        ProviderCredentialProfileKey chatCredentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddressFactory);

        return ProviderOperationProfileRegistration.RegisterDefaultOperationProfilesFromServices(
            services,
            providerId,
            chatServiceSurface,
            baseAddressFactory,
            credentialSourceKey,
            chatEndpointProfileKey,
            chatCredentialProfileKey,
            endpointId);
    }

    /// <summary>Registers default embedding endpoint and credential profiles from a runtime base address.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="embeddingServiceSurface">The embedding service surface.</param>
    /// <param name="baseAddressFactory">Resolves the embedding endpoint base address at composition time.</param>
    /// <param name="credentialSourceKey">The credential source key.</param>
    /// <param name="embeddingEndpointProfileKey">The embedding endpoint profile key.</param>
    /// <param name="embeddingCredentialProfileKey">The embedding credential profile key.</param>
    /// <param name="endpointId">The endpoint identity stamped on descriptors.</param>
    /// <returns>The captured embedding operation binding.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ProviderOperationBinding RegisterEmbeddingProfilesFromServices(
        IServiceCollection services,
        ProviderId providerId,
        ProviderServiceSurfaceId embeddingServiceSurface,
        Func<IServiceProvider, Uri> baseAddressFactory,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderEndpointProfileKey embeddingEndpointProfileKey,
        ProviderCredentialProfileKey embeddingCredentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddressFactory);

        return ProviderOperationProfileRegistration.RegisterDefaultOperationProfilesFromServices(
            services,
            providerId,
            embeddingServiceSurface,
            baseAddressFactory,
            credentialSourceKey,
            embeddingEndpointProfileKey,
            embeddingCredentialProfileKey,
            endpointId);
    }

    /// <summary>Registers default embedding endpoint and credential profiles.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="embeddingServiceSurface">The embedding service surface.</param>
    /// <param name="baseAddress">The embedding endpoint base address.</param>
    /// <param name="credentialSourceKey">The credential source key.</param>
    /// <param name="embeddingEndpointProfileKey">The embedding endpoint profile key.</param>
    /// <param name="embeddingCredentialProfileKey">The embedding credential profile key.</param>
    /// <param name="endpointId">The endpoint identity stamped on descriptors.</param>
    /// <returns>The captured embedding operation binding.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ProviderOperationBinding RegisterEmbeddingProfiles(
        IServiceCollection services,
        ProviderId providerId,
        ProviderServiceSurfaceId embeddingServiceSurface,
        Uri baseAddress,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderEndpointProfileKey embeddingEndpointProfileKey,
        ProviderCredentialProfileKey embeddingCredentialProfileKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        return ProviderOperationProfileRegistration.RegisterDefaultOperationProfiles(
            services,
            providerId,
            embeddingServiceSurface,
            baseAddress,
            credentialSourceKey,
            embeddingEndpointProfileKey,
            embeddingCredentialProfileKey,
            endpointId);
    }
}
