// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory;

/// <summary>Registers deterministic process-local artifact storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed in-memory artifact store that a profile's directory route can select.</summary>
        /// <param name="key">The backend key a profile route names; repeated registration of the same key is ignored.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/> and a <see cref="TimeProvider"/>. All state is explicitly process-local and is lost when the process ends.</remarks>
        public IServiceCollection AddInMemoryArtifactStore(ArtifactBackendKey key)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
            services.TryAddKeyedSingleton<IArtifactStore>(key.Value, static (provider, _) => new InMemoryArtifactStore(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetService<ILogger<InMemoryArtifactStore>>()));
            return services;
        }

        /// <summary>Registers the in-memory reference-commit intent store a coordinator reconciles against.</summary>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>The caller that commits artifact references owns this store and its transaction. State is explicitly process-local; repeated registration is ignored.</remarks>
        public IServiceCollection AddInMemoryArtifactReferenceCommitIntentStore()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IArtifactReferenceCommitIntentStore, InMemoryArtifactReferenceCommitIntentStore>();
            return services;
        }
    }
}
