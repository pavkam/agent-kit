// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite;

/// <summary>Registers durable host-local SQLite evaluation result storage.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers one keyed SQLite evaluation result store over an explicit host-authorized database.</summary>
        /// <param name="key">The registration key a plan selects; repeated registration of the same key is ignored.</param>
        /// <param name="target">The exact database and bootstrap effects the host authorizes.</param>
        /// <param name="configure">Optional lock timeout and size bound.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default or blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
        /// <remarks>The store is a singleton. It opens its database lazily on first use; call <see cref="SqliteEvaluationResultStore.InitializeAsync"/> at host startup to surface a misconfigured database early. No persistence target is ever chosen implicitly.</remarks>
        public IServiceCollection AddSqliteEvaluationResultStore(EvaluationResultStoreKey key, SqliteEvaluationStoreTarget target, Action<SqliteEvaluationStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new SqliteEvaluationStoreOptions();
            configure?.Invoke(options);
            var settings = new SqliteEvaluationStoreSettings(options.LockTimeout, options.MaximumRecordBytes);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddKeyedSingleton<IEvaluationResultStore>(key.Value, (provider, _) => new SqliteEvaluationResultStore(
                target,
                settings,
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<SqliteEvaluationResultStore>>()));
            return services;
        }
    }
}
