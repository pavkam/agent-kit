// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Sqlite;

/// <summary>Registers durable host-local SQLite artifact storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed SQLite artifact store over an explicit host-authorized database.</summary>
        /// <param name="key">The backend key a profile route names; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact database and bootstrap effects the host authorizes; it must not be shared with another store.</param>
        /// <param name="configure">Optional lock timeout and size bound.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its database lazily on first use and holds it exclusively; call <see cref="SqliteArtifactStore.InitializeAsync"/> at host startup to surface a misconfigured database early. The container disposes the store, releasing the file. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteArtifactStore(ArtifactBackendKey key, SqliteArtifactTarget target, Action<SqliteArtifactOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new SqliteArtifactOptions();
            configure?.Invoke(options);
            var settings = new SqliteArtifactSettings(options.LockTimeout, options.MaximumRecordBytes);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidEnforcementIntentIdGenerator>();
            services.TryAddKeyedSingleton<IArtifactStore>(key.Value, (provider, _) => new SqliteArtifactStore(
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteArtifactStore>>()));
            return services;
        }

        /// <summary>Registers the durable SQLite reference-commit intent store a coordinator reconciles against.</summary>
        /// <param name="target">The exact database and bootstrap effects the host authorizes; it must not be shared with an artifact store or another intent store.</param>
        /// <param name="configure">Optional lock timeout and size bound.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The caller that commits artifact references owns this store and its transaction. The store is a singleton that opens its database lazily on first use and holds it exclusively; call <see cref="SqliteArtifactReferenceCommitIntentStore.InitializeAsync"/> at host startup to surface a misconfigured database early. The container disposes the store, releasing the file. Repeated registration is ignored, and no persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteArtifactReferenceCommitIntentStore(SqliteArtifactTarget target, Action<SqliteArtifactOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new SqliteArtifactOptions();
            configure?.Invoke(options);
            var settings = new SqliteArtifactSettings(options.LockTimeout, options.MaximumRecordBytes);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IArtifactReferenceCommitIntentStore>(provider => new SqliteArtifactReferenceCommitIntentStore(
                target,
                settings,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteArtifactReferenceCommitIntentStore>>()));
            return services;
        }
    }
}
