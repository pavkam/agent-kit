// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Registers keyed provider credential sources for profile runtime selection.</summary>
public static class ProviderCredentialSourceRegistration
{
    /// <summary>Registers one credential source under both the profile source key and provider id.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="credentialSourceKey">The credential profile source key.</param>
    /// <param name="providerId">The provider identity key.</param>
    /// <param name="credentialSource">The credential source instance.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static IServiceCollection RegisterDualKeyCredentialSource(
        IServiceCollection services,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderId providerId,
        IProviderCredentialSource credentialSource)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(credentialSource);

        services.TryAddKeyedSingleton(credentialSourceKey, credentialSource);
        services.TryAddKeyedSingleton(providerId, credentialSource);
        return services;
    }
}
