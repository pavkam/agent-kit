// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json;

/// <summary>Registers durable inspectable host-local JSON memory, document, and vector storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed JSON durable-memory store over an explicit host-authorized root.</summary>
        /// <param name="key">The registration key a memory profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact root and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional bounds and encoding contract.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its root lazily on first use; call <see cref="JsonMemoryStore.InitializeAsync"/> at host startup to surface a misconfigured root early. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddJsonMemoryStore(MemoryStoreKey key, JsonMemoryTarget target, Action<JsonMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var settings = Settings(configure);
            AddShared(services);
            services.TryAddKeyedSingleton<IMemoryStore>(key.Value, (provider, _) => new JsonMemoryStore(
                key,
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<JsonMemoryStore>>()));
            return services;
        }

        /// <summary>Registers one keyed JSON document store over an explicit host-authorized root.</summary>
        /// <param name="key">The registration key a memory profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact root and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional bounds and encoding contract.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its root lazily on first use. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddJsonDocumentStore(DocumentStoreKey key, JsonMemoryTarget target, Action<JsonMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var settings = Settings(configure);
            AddShared(services);
            services.TryAddKeyedSingleton<IDocumentStore>(key.Value, (provider, _) => new JsonDocumentStore(
                key,
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<JsonDocumentStore>>()));
            return services;
        }

        /// <summary>Registers one keyed JSON brute-force vector index for exactly one vector space over an explicit host-authorized root.</summary>
        /// <param name="space">The space the index holds; its index key is the registration key a memory profile selects. Repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact root and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional bounds and encoding contract.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="space"/>, or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is not positive.</exception>
        /// <remarks>The index is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its root lazily on first use. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddJsonVectorIndex(VectorSpaceDescriptor space, JsonMemoryTarget target, Action<JsonMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(space);
            ArgumentNullException.ThrowIfNull(target);
            var settings = Settings(configure);
            AddShared(services);
            services.TryAddKeyedSingleton<IVectorIndex>(space.IndexKey.Value, (provider, _) => new JsonVectorIndex(
                space,
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<JsonVectorIndex>>()));
            return services;
        }
    }

    private static JsonMemorySettings Settings(Action<JsonMemoryOptions>? configure)
    {
        var options = new JsonMemoryOptions();
        configure?.Invoke(options);
        return new JsonMemorySettings(options.MaximumRecordBytes, options.MaximumDocumentBytes, options.CompactionRecordThreshold, options.Encoding);
    }

    private static void AddShared(IServiceCollection services)
    {
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
    }
}
