// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite;

/// <summary>Registers the durable local SQLite security-grant storage leaf.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Adds one SQLite grant-store adapter whose bounds come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed target and bootstrap effect policy.</param>
        /// <param name="configure">
        /// An optional delegate that mutates a fresh <see cref="SqliteSecurityGrantStoreOptions"/> before its values are
        /// materialized into immutable <see cref="SqliteSecurityGrantStoreSettings"/>. When null, the registration uses
        /// <see cref="SqliteSecurityGrantStoreSettings.CreateDefault"/>-equivalent bounds.
        /// </param>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The configured options contain a bound that is not positive or a lock timeout that is not a positive
        /// whole-second duration representable by the SQLite provider.
        /// </exception>
        /// <exception cref="InvalidOperationException">The same leaf was already configured with different target or effective settings evidence.</exception>
        /// <remarks>
        /// <para>
        /// The target is a required external persistence fact and is never defaulted. The configure delegate is
        /// optional; it runs exactly once, synchronously, during registration, and the options instance it receives is
        /// never registered in dependency injection. This is a security control-plane store, so validation stays eager:
        /// invalid values throw <see cref="ArgumentOutOfRangeException"/> at registration before any service is added.
        /// </para>
        /// <para>
        /// The effective settings are compared by value with any previously captured registration. Repeating the
        /// registration with equal effective settings, through either overload, is idempotent; repeating it with
        /// different effective settings throws <see cref="InvalidOperationException"/> without mutating the collection.
        /// See <see cref="AddSqliteSecurityGrantStore(IServiceCollection, SqliteSecurityGrantStoreTarget, SqliteSecurityGrantStoreSettings)"/>
        /// for the shared selection semantics.
        /// </para>
        /// </remarks>
        public IServiceCollection AddSqliteSecurityGrantStore(
            SqliteSecurityGrantStoreTarget target,
            Action<SqliteSecurityGrantStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new SqliteSecurityGrantStoreOptions();
            configure?.Invoke(options);
            var settings = new SqliteSecurityGrantStoreSettings(
                options.LockTimeout,
                options.MaximumGrantBytes,
                options.MaximumEnforcementBytes,
                options.MaximumResources,
                options.MaximumClaims,
                options.MaximumDelegationLinks);
            return services.AddSqliteSecurityGrantStore(target, settings);
        }

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

        /// <summary>Adds one SQLite security-decision-store adapter whose bounds come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed target and bootstrap effect policy.</param>
        /// <param name="configure">An optional delegate that mutates a fresh <see cref="SqliteSecurityDecisionStoreOptions"/>.</param>
        /// <returns>The same collection for chaining.</returns>
        public IServiceCollection AddSqliteSecurityDecisionStore(
            SqliteSecurityDecisionStoreTarget target,
            Action<SqliteSecurityDecisionStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new SqliteSecurityDecisionStoreOptions();
            configure?.Invoke(options);
            return services.AddSqliteSecurityDecisionStore(target, new SqliteSecurityDecisionStoreSettings(
                options.LockTimeout,
                options.MaximumDecisionBytes));
        }

        /// <summary>Adds one explicitly configured SQLite security-decision-store adapter without opening its target.</summary>
        /// <param name="target">The immutable fixed target and bootstrap effect policy.</param>
        /// <param name="settings">The immutable operational and codec bounds.</param>
        /// <returns>The same collection for chaining.</returns>
        public IServiceCollection AddSqliteSecurityDecisionStore(
            SqliteSecurityDecisionStoreTarget target,
            SqliteSecurityDecisionStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            var existingTarget = GetCapturedDecision<SqliteSecurityDecisionStoreTarget>(services);
            var existingSettings = GetCapturedDecision<SqliteSecurityDecisionStoreSettings>(services);
            if (existingTarget is not null && existingTarget != target)
            {
                throw new InvalidOperationException("The SQLite decision-store leaf is already configured for a different target.");
            }
            if (existingSettings is not null && existingSettings != settings)
            {
                throw new InvalidOperationException("The SQLite decision-store leaf is already configured with different settings.");
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
                    descriptor.ServiceType == typeof(ISecurityDecisionStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(SqliteSecurityDecisionStore)))
            {
                services.Add(ServiceDescriptor.Singleton<ISecurityDecisionStore, SqliteSecurityDecisionStore>());
            }

            return services;

            static T? GetCapturedDecision<T>(IServiceCollection source)
                where T : class
            {
                var matches = source.Where(static descriptor =>
                    descriptor.ServiceType == typeof(T) && descriptor.ServiceKey is null).ToArray();
                return matches.Length switch
                {
                    0 => null,
                    1 when GetInstance(matches[0]) is T value => value,
                    _ => throw new InvalidOperationException(
                        $"The SQLite decision-store {typeof(T).Name} registration is not one exact captured instance."),
                };

                static object? GetInstance(ServiceDescriptor descriptor) => descriptor.IsKeyedService
                    ? descriptor.KeyedImplementationInstance
                    : descriptor.ImplementationInstance;
            }
        }

        /// <summary>Adds one SQLite approval-store adapter whose bounds come from an optional configure delegate.</summary>
        /// <param name="target">The immutable fixed target and bootstrap effect policy.</param>
        /// <param name="configure">An optional delegate that mutates a fresh <see cref="SqliteApprovalStoreOptions"/>.</param>
        /// <returns>The same collection for chaining.</returns>
        public IServiceCollection AddSqliteApprovalStore(
            SqliteApprovalStoreTarget target,
            Action<SqliteApprovalStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new SqliteApprovalStoreOptions();
            configure?.Invoke(options);
            return services.AddSqliteApprovalStore(target, new SqliteApprovalStoreSettings(
                options.LockTimeout,
                options.MaximumRecordBytes));
        }

        /// <summary>Adds one explicitly configured SQLite approval-store adapter without opening its target.</summary>
        /// <param name="target">The immutable fixed target and bootstrap effect policy.</param>
        /// <param name="settings">The immutable operational and codec bounds.</param>
        /// <returns>The same collection for chaining.</returns>
        public IServiceCollection AddSqliteApprovalStore(
            SqliteApprovalStoreTarget target,
            SqliteApprovalStoreSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            var existingTarget = GetCapturedApproval<SqliteApprovalStoreTarget>(services);
            var existingSettings = GetCapturedApproval<SqliteApprovalStoreSettings>(services);
            if (existingTarget is not null && existingTarget != target)
            {
                throw new InvalidOperationException("The SQLite approval-store leaf is already configured for a different target.");
            }
            if (existingSettings is not null && existingSettings != settings)
            {
                throw new InvalidOperationException("The SQLite approval-store leaf is already configured with different settings.");
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
                    descriptor.ServiceType == typeof(IApprovalStore)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(SqliteApprovalStore)))
            {
                services.Add(ServiceDescriptor.Singleton<IApprovalStore, SqliteApprovalStore>());
            }

            return services;

            static T? GetCapturedApproval<T>(IServiceCollection source)
                where T : class
            {
                var matches = source.Where(static descriptor =>
                    descriptor.ServiceType == typeof(T) && descriptor.ServiceKey is null).ToArray();
                return matches.Length switch
                {
                    0 => null,
                    1 when GetInstance(matches[0]) is T value => value,
                    _ => throw new InvalidOperationException(
                        $"The SQLite approval-store {typeof(T).Name} registration is not one exact captured instance."),
                };

                static object? GetInstance(ServiceDescriptor descriptor) => descriptor.IsKeyedService
                    ? descriptor.KeyedImplementationInstance
                    : descriptor.ImplementationInstance;
            }
        }
    }
}
