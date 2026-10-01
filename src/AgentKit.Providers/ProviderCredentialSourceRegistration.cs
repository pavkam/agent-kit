// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using AgentKit.Providers.Credentials;
using AgentKit.Providers.Egress;

/// <summary>Registers keyed first-party provider credential sources for profile runtime selection.</summary>
/// <remarks>
/// Every source is registered only under its <see cref="ProviderCredentialSourceKey"/>; no source is ever registered or
/// resolved under a provider identity or without a key, so a credential profile can never be satisfied by another
/// account's source. Registration uses <c>TryAdd</c> semantics: the first registration for a key wins, and an application
/// replaces a source through <c>ReplaceProviderCredentialSource</c>.
/// </remarks>
public static class ProviderCredentialSourceRegistration
{
    /// <summary>
    /// Registers the shared <see cref="ProviderCredentialReadGate"/> and its identity generators with <c>TryAdd</c>
    /// semantics, so first-party and custom credential sources can enforce credential-read grants.
    /// </summary>
    /// <param name="services">The service collection that receives the gate.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <remarks>
    /// The gate resolves <see cref="ISecurityGrantStore"/>, <see cref="ISecurityAuditDispatcher"/>, and
    /// <see cref="TimeProvider"/> from the container when first used; the registration builds no provider.
    /// </remarks>
    public static IServiceCollection AddCredentialReadGate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
            new GuidIdentifierGenerator<SecurityEnforcementIntentId>(static value => new SecurityEnforcementIntentId(value)));
        services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>>(
            new GuidIdentifierGenerator<SecurityAuditRecordId>(static value => new SecurityAuditRecordId(value)));
        services.TryAddSingleton(static provider => new ProviderCredentialReadGate(
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<ISecurityAuditDispatcher>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }

    /// <summary>Registers a <see cref="StaticApiKeyCredentialSource"/> for <paramref name="credentialSourceKey"/>.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="credentialSourceKey">The source key the credential profile selects the source by.</param>
    /// <param name="apiKey">The non-empty API key text.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="credentialSourceKey"/> is the default value.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is null, empty, or whitespace.</exception>
    public static IServiceCollection AddStaticApiKeySource(
        IServiceCollection services,
        ProviderCredentialSourceKey credentialSourceKey,
        string apiKey)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(credentialSourceKey, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _ = AddCredentialReadGate(services);
        services.TryAddKeyedSingleton<IProviderCredentialSource>(
            credentialSourceKey,
            (provider, _) => new StaticApiKeyCredentialSource(
                credentialSourceKey,
                apiKey,
                provider.GetRequiredService<ProviderCredentialReadGate>()));
        return services;
    }

    /// <summary>
    /// Registers a <see cref="DelegatingOAuthCredentialSource"/> for <paramref name="credentialSourceKey"/> over the
    /// application's <typeparamref name="TProvider"/>, keyed by <paramref name="providerId"/>.
    /// </summary>
    /// <typeparam name="TProvider">The application-supplied OAuth access-token provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="credentialSourceKey">The source key the credential profile selects the source by.</param>
    /// <param name="providerId">The provider identity the token provider is registered under.</param>
    /// <returns>The same <paramref name="services"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="credentialSourceKey"/> or <paramref name="providerId"/> is the default value.</exception>
    public static IServiceCollection AddOAuthTokenSource<TProvider>(
        IServiceCollection services,
        ProviderCredentialSourceKey credentialSourceKey,
        ProviderId providerId)
        where TProvider : class, IOAuthAccessTokenProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(credentialSourceKey, default);
        ArgumentOutOfRangeException.ThrowIfEqual(providerId, default);

        _ = AddCredentialReadGate(services);
        services.TryAddKeyedSingleton<IOAuthAccessTokenProvider, TProvider>(providerId);
        services.TryAddKeyedSingleton<IProviderCredentialSource>(
            credentialSourceKey,
            (provider, _) => new DelegatingOAuthCredentialSource(
                credentialSourceKey,
                provider.GetRequiredKeyedService<IOAuthAccessTokenProvider>(providerId),
                provider.GetRequiredService<ProviderCredentialReadGate>()));
        return services;
    }
}
