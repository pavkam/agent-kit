// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Registers artifact coordination without fabricating a profile, storage backend, or persistence target.</summary>
/// <remarks>
/// Every method returns the same <see cref="IServiceCollection"/>, never builds a service provider, and is idempotent for an
/// identical registration. A conflicting registration under an already-used key throws <see cref="InvalidOperationException"/>;
/// the matching <c>Replace</c> method changes it deliberately. Event sinks are additive.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed default coordinator bound to a logical profile, plus its process-output sink.</summary>
        /// <param name="key">The coordinator key an agent definition selects.</param>
        /// <param name="profileKey">The logical profile the coordinator is bound to; it must also be registered through <c>AddArtifactProfile</c>.</param>
        /// <param name="configure">Optional mechanics configuration; it is validated and captured into an immutable snapshot now.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException">A key is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered with a different profile or mechanics.</exception>
        /// <remarks>Repeating the call for one key is idempotent. The first coordinator key also backs the unkeyed process-output sink. No profile or store is registered implicitly.</remarks>
        public IServiceCollection AddAgentArtifacts(
            ComponentKey<IArtifactCoordinator> key,
            ArtifactProfileKey profileKey,
            Action<AgentArtifactOptions>? configure = null) =>
            ArtifactRegistration.AddDefault(services, key, profileKey, configure);

        /// <summary>Registers one revision of a logical artifact profile.</summary>
        /// <param name="key">The profile key.</param>
        /// <param name="configure">The routes, default retention, and admitted content; captured into an immutable snapshot now.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The profile is invalid, or this version is already registered with different content.</exception>
        /// <remarks>A new <see cref="ArtifactProfileOptions.Version"/> under the same key is retained beside earlier ones, so references created under older revisions keep resolving.</remarks>
        public IServiceCollection AddArtifactProfile(ArtifactProfileKey key, Action<ArtifactProfileOptions> configure) =>
            ArtifactRegistration.AddProfile(services, key, configure, replace: false);

        /// <summary>Registers a profile revision, replacing an earlier registration of the same version.</summary>
        /// <param name="key">The profile key.</param>
        /// <param name="configure">The replacement configuration.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The profile is invalid.</exception>
        public IServiceCollection ReplaceArtifactProfile(ArtifactProfileKey key, Action<ArtifactProfileOptions> configure) =>
            ArtifactRegistration.AddProfile(services, key, configure, replace: true);

        /// <summary>Registers an artifact store under a stable backend key.</summary>
        /// <typeparam name="TStore">The store implementation.</typeparam>
        /// <param name="key">The backend key a profile route names.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">A different store is already registered under the key.</exception>
        public IServiceCollection AddArtifactStore<TStore>(ArtifactBackendKey key)
            where TStore : class, IArtifactStore =>
            ArtifactRegistration.AddStore<TStore>(services, key, replace: false);

        /// <summary>Registers an artifact store under a backend key, replacing any earlier registration of that key.</summary>
        /// <typeparam name="TStore">The store implementation.</typeparam>
        /// <param name="key">The backend key a profile route names.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection ReplaceArtifactStore<TStore>(ArtifactBackendKey key)
            where TStore : class, IArtifactStore =>
            ArtifactRegistration.AddStore<TStore>(services, key, replace: true);

        /// <summary>Registers an additive lifecycle event sink for one coordinator.</summary>
        /// <typeparam name="TSink">The sink implementation.</typeparam>
        /// <param name="coordinatorKey">The coordinator whose events the sink observes.</param>
        /// <param name="registration">The sink identity, delivery order, and service lifetime.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="coordinatorKey"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The sink identity is already registered for the coordinator differently.</exception>
        /// <remarks>Sinks are delivered in order then identity, resolved per delivery under their declared lifetime, and isolated from lifecycle outcomes.</remarks>
        public IServiceCollection AddArtifactEventSink<TSink>(
            ComponentKey<IArtifactCoordinator> coordinatorKey,
            ArtifactEventSinkRegistration registration)
            where TSink : class, IArtifactEventSink =>
            ArtifactRegistration.AddEventSink<TSink>(services, coordinatorKey, registration);

        /// <summary>Replaces the coordinator registered under a key.</summary>
        /// <typeparam name="TCoordinator">The replacement coordinator.</typeparam>
        /// <param name="key">The coordinator key.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection ReplaceArtifactCoordinator<TCoordinator>(ComponentKey<IArtifactCoordinator> key)
            where TCoordinator : class, IArtifactCoordinator =>
            ArtifactRegistration.ReplaceCoordinator<TCoordinator>(services, key);

        /// <summary>Replaces the engine-wide artifact integrity validator.</summary>
        /// <typeparam name="TValidator">The stateless, thread-safe validator.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceArtifactIntegrityValidator<TValidator>()
            where TValidator : class, IArtifactIntegrityValidator =>
            ArtifactRegistration.ReplaceSingleton<IArtifactIntegrityValidator, TValidator>(services);

        /// <summary>Replaces the engine-wide artifact retention policy.</summary>
        /// <typeparam name="TPolicy">The stateless, thread-safe policy.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceArtifactRetentionPolicy<TPolicy>()
            where TPolicy : class, IArtifactRetentionPolicy =>
            ArtifactRegistration.ReplaceSingleton<IArtifactRetentionPolicy, TPolicy>(services);
    }
}
