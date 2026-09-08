// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Registers the durable local SQLite security-grant storage leaf.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds one explicitly configured SQLite grant-store adapter without opening its target.</summary>
        /// <param name="target">The immutable fixed target and bootstrap effect policy.</param>
        /// <param name="settings">The immutable operational and codec bounds.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="target"/>, or <paramref name="settings"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or settings evidence.</exception>
        /// <remarks>
        /// Repeating the exact registration is idempotent. Other leaf or custom <see cref="ISecurityGrantStore"/>
        /// registrations remain visible so composition rejects ambiguity. Trusted bootstrap resolves the selected interface,
        /// verifies it is <see cref="SqliteSecurityGrantStore"/>, and calls <see cref="SqliteSecurityGrantStore.InitializeAsync"/>.
        /// </remarks>
        public IServiceCollection AddSqliteSecurityGrantStore(
            SqliteSecurityGrantStoreTarget target,
            SqliteSecurityGrantStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            var existingTarget = GetCaptured<SqliteSecurityGrantStoreTarget>(services);
            var existingSettings = GetCaptured<SqliteSecurityGrantStoreSettings>(services);
            if (existingTarget is not null && existingTarget != target)
            {
                throw new InvalidOperationException("The SQLite grant-store leaf is already configured for a different target.");
            }
            if (existingSettings is not null && existingSettings != settings)
            {
                throw new InvalidOperationException("The SQLite grant-store leaf is already configured with different settings.");
            }

            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            if (existingTarget is null)
            {
                _ = services.AddSingleton(target);
            }
            if (existingSettings is null)
            {
                _ = services.AddSingleton(settings);
            }
            if (!services.Any(static descriptor =>
                    descriptor.ServiceType == typeof(ISecurityGrantStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(SqliteSecurityGrantStore)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityGrantStore, SqliteSecurityGrantStore>());
            }
            return services;

            static T? GetCaptured<T>(IServiceCollection source)
                where T : class
            {
                var matches = source.Where(static descriptor =>
                    descriptor.ServiceType == typeof(T) && descriptor.ServiceKey is null).ToArray();
                return matches.Length switch
                {
                    0 => null,
                    1 when GetInstance(matches[0]) is T value => value,
                    _ => throw new InvalidOperationException(
                        $"The SQLite grant-store {typeof(T).Name} registration is not one exact captured instance."),
                };

                static object? GetInstance(ServiceDescriptor descriptor) => descriptor.IsKeyedService
                    ? descriptor.KeyedImplementationInstance
                    : descriptor.ImplementationInstance;
            }
        }
    }
}
