// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers default endpoint and credential profiles for one provider operation surface.
/// </summary>
public static class ProviderOperationProfileRegistration
{
    /// <summary>
    /// Registers one endpoint and credential profile pair and returns the binding used by descriptors.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="providerId">The provider identity served by the endpoint.</param>
    /// <param name="serviceSurface">The service surface identity.</param>
    /// <param name="baseAddress">The absolute base address for the endpoint profile.</param>
    /// <param name="credentialSourceKey">The keyed credential source referenced by the credential profile.</param>
    /// <param name="endpointKey">The stable endpoint profile key.</param>
    /// <param name="credentialKey">The stable credential profile key.</param>
    /// <param name="endpointId">The endpoint identity stamped onto descriptors.</param>
    /// <returns>The captured operation binding for descriptor publication.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ProviderOperationBinding RegisterDefaultOperationProfiles(
        IServiceCollection services,
        ProviderId providerId,
        ProviderServiceSurfaceId serviceSurface,
        Uri baseAddress,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderEndpointProfileKey endpointKey,
        ProviderCredentialProfileKey credentialKey,
        ProviderEndpointId endpointId)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        var endpointVersion = new ProviderEndpointProfileVersion(1);
        var credentialVersion = new ProviderCredentialProfileVersion(1);

        _ = services.AddProviderEndpointProfile(endpointKey, options =>
        {
            options.ProviderId = providerId;
            options.ServiceSurface = serviceSurface;
            options.EndpointId = endpointId;
            options.BaseAddress = baseAddress;
            options.Version = endpointVersion;
        });

        _ = services.AddProviderCredentialProfile(credentialKey, options =>
        {
            options.ProviderId = providerId;
            options.ServiceSurface = serviceSurface;
            options.SourceKey = credentialSourceKey;
            options.Version = credentialVersion;
        });

        return new ProviderOperationBinding(
            new ProviderEndpointProfileReference(endpointKey, endpointVersion),
            new ProviderCredentialProfileReference(credentialKey, credentialVersion));
    }
}
