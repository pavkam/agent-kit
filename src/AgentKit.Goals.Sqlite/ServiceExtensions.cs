// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite;

/// <summary>Registers durable host-local SQLite goal storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed SQLite goal store over an explicit host-authorized database.</summary>
        /// <param name="key">The registration key a goal profile selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact database and bootstrap effects the host authorizes.</param>
        /// <param name="configure">Optional lock timeout, size bound, and intent scanners.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The store is a singleton that requires an <see cref="ISecurityGrantStore"/>. It opens its database lazily on first use; call <see cref="SqliteGoalStore.InitializeAsync"/> at host startup to surface a misconfigured database early. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteGoalStore(GoalStoreKey key, SqliteGoalStoreTarget target, Action<SqliteGoalStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new SqliteGoalStoreOptions();
            configure?.Invoke(options);
            var settings = new SqliteGoalStoreSettings(options.LockTimeout, options.MaximumRecordBytes, options.AuthorizedIntentScanners);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddKeyedSingleton<IGoalStore>(key.Value, (provider, _) => new SqliteGoalStore(
                target,
                settings,
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteGoalStore>>()));
            return services;
        }
    }
}
