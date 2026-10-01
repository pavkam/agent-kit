// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json;

/// <summary>Registers durable inspectable host-local JSON artifact storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed JSON artifact store over an explicit host-authorized root.</summary>
        /// <param name="key">The backend key a profile route names; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact root and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional bounds and encoding contract.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its root lazily on first use and locks it; call <see cref="JsonArtifactStore.InitializeAsync"/> at host startup to surface a misconfigured root early. The container disposes the store, releasing the lock. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddJsonArtifactStore(ArtifactBackendKey key, JsonArtifactTarget target, Action<JsonArtifactOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new JsonArtifactOptions();
            configure?.Invoke(options);
            var settings = new JsonArtifactSettings(
                options.MaximumRecordBytes, options.MaximumDocumentBytes, options.MaximumPayloadBytes, options.CompactionRecordThreshold, options.Encoding);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
            services.TryAddKeyedSingleton<IArtifactStore>(key.Value, (provider, _) => new JsonArtifactStore(
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<JsonArtifactStore>>()));
            return services;
        }
    }
}
