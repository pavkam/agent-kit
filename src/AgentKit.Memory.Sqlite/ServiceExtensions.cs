// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Sqlite;

/// <summary>Registers durable host-local SQLite memory, document, and vector storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed SQLite durable-memory store over an explicit host-authorized database.</summary>
        /// <param name="key">The registration key a memory profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact database and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional lock timeout and size bound.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its database lazily on first use; call <see cref="SqliteMemoryStore.InitializeAsync"/> at host startup to surface a misconfigured database early. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteMemoryStore(MemoryStoreKey key, SqliteMemoryTarget target, Action<SqliteMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var settings = Settings(configure);
            AddShared(services);
            services.TryAddKeyedSingleton<IMemoryStore>(key.Value, (provider, _) => new SqliteMemoryStore(
                key,
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteMemoryStore>>()));
            return services;
        }

        /// <summary>Registers one keyed SQLite document store over an explicit host-authorized database.</summary>
        /// <param name="key">The registration key a memory profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact database and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional lock timeout and size bound.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its database lazily on first use. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteDocumentStore(DocumentStoreKey key, SqliteMemoryTarget target, Action<SqliteMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var settings = Settings(configure);
            AddShared(services);
            services.TryAddKeyedSingleton<IDocumentStore>(key.Value, (provider, _) => new SqliteDocumentStore(
                key,
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteDocumentStore>>()));
            return services;
        }

        /// <summary>Registers one keyed SQLite brute-force vector index for exactly one vector space over an explicit host-authorized database.</summary>
        /// <param name="space">The space the index holds; its index key is the registration key a memory profile selects. Repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact database and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional lock timeout and size bound.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="space"/>, or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The index is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its database lazily on first use. It advertises an exact scan only and never approximate search. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteVectorIndex(VectorSpaceDescriptor space, SqliteMemoryTarget target, Action<SqliteMemoryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(space);
            ArgumentNullException.ThrowIfNull(target);
            var settings = Settings(configure);
            AddShared(services);
            services.TryAddKeyedSingleton<IVectorIndex>(space.IndexKey.Value, (provider, _) => new SqliteVectorIndex(
                space,
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteVectorIndex>>()));
            return services;
        }
    }

    private static SqliteMemorySettings Settings(Action<SqliteMemoryOptions>? configure)
    {
        var options = new SqliteMemoryOptions();
        configure?.Invoke(options);
        return new SqliteMemorySettings(options.LockTimeout, options.MaximumRecordBytes);
    }

    private static void AddShared(IServiceCollection services)
    {
        _ = services.AddAgentKitObservability();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
    }
}
