// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Implements side-effect-free registration of keyed coordinators, profiles, stores, and event sinks.</summary>
/// <remarks>
/// Every method validates and captures its configuration immediately, never builds a service provider, and is idempotent for an
/// identical registration. A conflicting registration under an already-used key throws <see cref="InvalidOperationException"/>
/// unless the matching replacement method was called. No method registers a concrete store, profile, or persistence target.
/// </remarks>
internal static class ArtifactRegistration
{
    /// <summary>Registers one keyed default coordinator and its process-output sink.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The coordinator key.</param>
    /// <param name="profileKey">The logical profile the coordinator is bound to.</param>
    /// <param name="configure">Optional mechanics configuration, captured now.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException">A key is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    /// <exception cref="InvalidOperationException">The key is already registered with a different profile or mechanics.</exception>
    internal static IServiceCollection AddDefault(
        IServiceCollection services,
        ComponentKey<IArtifactCoordinator> key,
        ArtifactProfileKey profileKey,
        Action<AgentArtifactOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey.Value, nameof(profileKey));
        var options = new AgentArtifactOptions();
        configure?.Invoke(options);
        var registration = new ArtifactCoordinatorRegistration(key, profileKey, AgentArtifactOptionsSnapshot.Create(options));
        var existing = services
            .Where(static descriptor => descriptor.ServiceType == typeof(ArtifactCoordinatorRegistration))
            .Select(static descriptor => (ArtifactCoordinatorRegistration) descriptor.ImplementationInstance!)
            .FirstOrDefault(candidate => candidate.Key == key);
        if (existing is not null)
        {
            return existing == registration
                ? services
                : throw new InvalidOperationException($"Artifact coordinator '{key}' is already registered with a different profile or mechanics.");
        }

        _ = services.AddAgentKitObservability();
        AddDefaults(services);
        _ = services.AddSingleton(registration);
        var serviceKey = key.Value;
        services.TryAddKeyedSingleton<IArtifactCoordinator>(serviceKey, (provider, _) =>
        {
            var versions = ProfileVersions(provider, profileKey);
            return new ArtifactCoordinator(
                key,
                versions.OrderByDescending(static version => version.Version.Value).First(),
                versions,
                provider.GetRequiredService<IArtifactStoreSelector>(),
                provider.GetRequiredService<IArtifactIntegrityValidator>(),
                provider.GetRequiredService<IArtifactRetentionPolicy>(),
                provider.GetRequiredService<ISecurityAuthoritySelector>(),
                provider.GetRequiredService<IArtifactEventDispatcher>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IIdentifierGenerator<ArtifactId>>(),
                provider.GetRequiredService<IIdentifierGenerator<ArtifactPreparationId>>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityRequestId>>(),
                registration.Options,
                provider.GetService<IArtifactReferenceCommitIntentStore>(),
                provider.GetService<ILogger<ArtifactCoordinator>>());
        });
        services.TryAddKeyedSingleton<IProcessOutputArtifactSink>(serviceKey, (provider, _) =>
        {
            var versions = ProfileVersions(provider, profileKey);
            return new ArtifactProcessOutputSink(
                provider.GetRequiredKeyedService<IArtifactCoordinator>(serviceKey),
                versions.OrderByDescending(static version => version.Version.Value).First().DefaultDirectory,
                registration.Options);
        });
        services.TryAddSingleton(provider => provider.GetRequiredKeyedService<IProcessOutputArtifactSink>(serviceKey));
        return services;
    }

    /// <summary>Registers or replaces one profile revision.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The profile key.</param>
    /// <param name="configure">The profile configuration, captured now.</param>
    /// <param name="replace">Whether an existing revision with the same version is replaced.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The profile is invalid, or the same version is already registered with different content and <paramref name="replace"/> is <see langword="false"/>.</exception>
    internal static IServiceCollection AddProfile(
        IServiceCollection services,
        ArtifactProfileKey key,
        Action<ArtifactProfileOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ArtifactProfileOptions();
        configure(options);
        var snapshot = ArtifactProfileSnapshot.Create(key, options);
        var sameVersion = services
            .Where(descriptor => descriptor.ServiceType == typeof(ArtifactProfileSnapshot) && Equals(descriptor.ServiceKey, key.Value)
                && descriptor.KeyedImplementationInstance is ArtifactProfileSnapshot existing && existing.Version == snapshot.Version)
            .ToArray();
        if (sameVersion.Length > 0)
        {
            if (!replace)
            {
                return sameVersion.All(descriptor => snapshot.Equals((ArtifactProfileSnapshot) descriptor.KeyedImplementationInstance!))
                    ? services
                    : throw new InvalidOperationException($"Artifact profile '{key}' version {snapshot.Version.Value} is already registered with different content.");
            }

            foreach (var descriptor in sameVersion)
            {
                _ = services.Remove(descriptor);
            }
        }

        return services.AddKeyedSingleton(key.Value, snapshot);
    }

    /// <summary>Registers or replaces one keyed artifact store.</summary>
    /// <typeparam name="TStore">The store implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The backend key.</param>
    /// <param name="replace">Whether an existing registration is replaced.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">A different store is already registered under the key and <paramref name="replace"/> is <see langword="false"/>.</exception>
    internal static IServiceCollection AddStore<TStore>(IServiceCollection services, ArtifactBackendKey key, bool replace)
        where TStore : class, IArtifactStore
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        var existing = services.Where(descriptor => descriptor.ServiceType == typeof(IArtifactStore) && Equals(descriptor.ServiceKey, key.Value)).ToArray();
        if (existing.Length > 0)
        {
            if (!replace)
            {
                return existing.All(static descriptor => descriptor.KeyedImplementationType == typeof(TStore))
                    ? services
                    : throw new InvalidOperationException($"A different artifact store is already registered under backend key '{key}'.");
            }

            foreach (var descriptor in existing)
            {
                _ = services.Remove(descriptor);
            }
        }

        return services.AddKeyedSingleton<IArtifactStore, TStore>(key.Value);
    }

    /// <summary>Registers one additive event sink for a coordinator.</summary>
    /// <typeparam name="TSink">The sink implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="coordinatorKey">The coordinator whose events the sink observes.</param>
    /// <param name="registration">The sink identity, order, and lifetime.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="coordinatorKey"/> is blank.</exception>
    /// <exception cref="InvalidOperationException">The sink identity is already registered for the coordinator with a different type, order, or lifetime.</exception>
    internal static IServiceCollection AddEventSink<TSink>(
        IServiceCollection services,
        ComponentKey<IArtifactCoordinator> coordinatorKey,
        ArtifactEventSinkRegistration registration)
        where TSink : class, IArtifactEventSink
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinatorKey.Value, nameof(coordinatorKey));
        ArgumentNullException.ThrowIfNull(registration);
        var declaration = new ArtifactEventSinkDeclaration(coordinatorKey, registration, typeof(TSink));
        var existing = services
            .Where(static descriptor => descriptor.ServiceType == typeof(ArtifactEventSinkDeclaration))
            .Select(static descriptor => (ArtifactEventSinkDeclaration) descriptor.ImplementationInstance!)
            .FirstOrDefault(candidate => candidate.CoordinatorKey == coordinatorKey && candidate.Registration.Id == registration.Id);
        if (existing is not null)
        {
            return existing == declaration
                ? services
                : throw new InvalidOperationException($"Artifact event sink '{registration.Id}' is already registered for coordinator '{coordinatorKey}' differently.");
        }

        services.TryAdd(new ServiceDescriptor(typeof(TSink), typeof(TSink), registration.Lifetime));
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton<IArtifactEventDispatcher, ArtifactEventDispatcher>();
        return services.AddSingleton(declaration);
    }

    /// <summary>Replaces the coordinator registered under a key.</summary>
    /// <typeparam name="TCoordinator">The replacement coordinator.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="key">The coordinator key.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
    internal static IServiceCollection ReplaceCoordinator<TCoordinator>(IServiceCollection services, ComponentKey<IArtifactCoordinator> key)
        where TCoordinator : class, IArtifactCoordinator
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IArtifactCoordinator>(services, key.Value);
        return services.AddKeyedSingleton<IArtifactCoordinator, TCoordinator>(key.Value);
    }

    /// <summary>Replaces the unkeyed singleton registered for a service contract.</summary>
    /// <typeparam name="TService">The contract.</typeparam>
    /// <typeparam name="TImplementation">The replacement implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceSingleton<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        for (var index = services.Count - 1; index >= 0; index--)
        {
            if (services[index].ServiceType == typeof(TService) && services[index].ServiceKey is null)
            {
                services.RemoveAt(index);
            }
        }

        return services.AddSingleton<TService, TImplementation>();
    }

    private static void AddDefaults(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<ArtifactId>, GuidArtifactIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<ArtifactPreparationId>, GuidArtifactPreparationIdGenerator>();
        services.TryAddSingleton<IArtifactStoreSelector, DefaultArtifactStoreSelector>();
        services.TryAddSingleton<IArtifactIntegrityValidator, DefaultArtifactIntegrityValidator>();
        services.TryAddSingleton<IArtifactRetentionPolicy, DefaultArtifactRetentionPolicy>();
        services.TryAddSingleton<IArtifactEventDispatcher, ArtifactEventDispatcher>();
        services.TryAddSingleton<IArtifactCoordinatorCatalog>(static provider => new DefaultArtifactCoordinatorCatalog(
            provider.GetServices<ArtifactCoordinatorRegistration>(), provider));
    }

    /// <summary>Resolves every retained revision of a profile.</summary>
    /// <param name="provider">The provider holding keyed profile snapshots.</param>
    /// <param name="profileKey">The profile key.</param>
    /// <returns>At least one immutable revision.</returns>
    /// <exception cref="InvalidOperationException">The profile is not registered.</exception>
    internal static ImmutableArray<ArtifactProfileSnapshot> ProfileVersions(IServiceProvider provider, ArtifactProfileKey profileKey)
    {
        var versions = provider.GetKeyedServices<ArtifactProfileSnapshot>(profileKey.Value).ToImmutableArray();
        return versions.IsEmpty
            ? throw new InvalidOperationException($"Artifact profile '{profileKey}' is not registered. Call AddArtifactProfile before resolving a coordinator.")
            : versions;
    }

    private static void RemoveKeyed<TService>(IServiceCollection services, object serviceKey)
    {
        for (var index = services.Count - 1; index >= 0; index--)
        {
            if (services[index].ServiceType == typeof(TService) && Equals(services[index].ServiceKey, serviceKey))
            {
                services.RemoveAt(index);
            }
        }
    }
}
