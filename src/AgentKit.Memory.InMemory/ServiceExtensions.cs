// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.InMemory;

/// <summary>Registers explicitly ephemeral process-local memory, document, and vector storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed in-memory durable-memory store.</summary>
        /// <param name="key">The registration key a memory profile selects; repeated registration of the same key is ignored.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/> and never chooses itself as a persistence target: selecting it is the application's explicit act of accepting ephemeral memory.</remarks>
        public IServiceCollection AddInMemoryMemoryStore(MemoryStoreKey key)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            AddShared(services);
            services.TryAddKeyedSingleton<IMemoryStore>(key.Value, (provider, _) => new InMemoryMemoryStore(
                key,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<InMemoryMemoryStore>>()));
            return services;
        }

        /// <summary>Registers one keyed in-memory document store.</summary>
        /// <param name="key">The registration key a memory profile selects; repeated registration of the same key is ignored.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/> and never chooses itself as a persistence target.</remarks>
        public IServiceCollection AddInMemoryDocumentStore(DocumentStoreKey key)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            AddShared(services);
            services.TryAddKeyedSingleton<IDocumentStore>(key.Value, (provider, _) => new InMemoryDocumentStore(
                key,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<InMemoryDocumentStore>>()));
            return services;
        }

        /// <summary>Registers one keyed in-memory brute-force vector index for exactly one vector space.</summary>
        /// <param name="space">The space the index holds; its index key is the registration key a memory profile selects. Repeated registration of the same key is ignored.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="space"/> is null.</exception>
        /// <remarks>The index is a singleton that requires an <see cref="ISecurityGrantStore"/> and never chooses itself as a persistence target.</remarks>
        public IServiceCollection AddInMemoryVectorIndex(VectorSpaceDescriptor space)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(space);
            AddShared(services);
            services.TryAddKeyedSingleton<IVectorIndex>(space.IndexKey.Value, (provider, _) => new InMemoryVectorIndex(
                space,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<InMemoryVectorIndex>>()));
            return services;
        }
    }

    private static void AddShared(IServiceCollection services)
    {
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
    }
}
