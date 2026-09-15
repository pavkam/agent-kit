// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Sqlite;

/// <summary>Registers the durable host-local SQLite budget-ledger leaf.</summary>
public static class ServiceExtensions
{
    /// <summary>Adds SQLite budget-ledger registration operations to a caller-owned collection.</summary>
    /// <param name="services">The mutable application composition.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one singleton SQLite ledger whose bounds come from an optional configure delegate, without opening its target.</summary>
        /// <param name="target">The immutable fixed bootstrap target.</param>
        /// <param name="configure">An optional delegate that adjusts the mutable <see cref="SqliteBudgetLedgerOptions"/> before they are materialized into immutable settings. When null, the defaults equal <see cref="SqliteBudgetLedgerSettings.CreateDefault"/>.</param>
        /// <returns>The same caller-owned collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured options violate a <see cref="SqliteBudgetLedgerSettings"/> bound, such as a non-positive limit or a lock timeout that is not a positive whole-second duration.</exception>
        /// <exception cref="InvalidOperationException">This leaf already captured a different target or different effective settings.</exception>
        /// <remarks>
        /// <para>The <paramref name="target"/> is a required external persistence fact; this package never fabricates a database path or store identity as a default. Only the operational and evidence bounds have first-party defaults, and <paramref name="configure"/> is optional.</para>
        /// <para>The delegate runs synchronously during registration. Its result is converted to <see cref="SqliteBudgetLedgerSettings"/> immediately, so invalid values throw <see cref="ArgumentOutOfRangeException"/> here and no registration is added. The options instance itself is not registered; the ledger observes only the materialized immutable settings singleton.</para>
        /// <para>Repeating this call with a delegate that produces the same effective settings is idempotent. Repeating it with different effective settings, or with a different target, throws <see cref="InvalidOperationException"/> exactly as the explicit <see cref="SqliteBudgetLedgerSettings"/> overload does, because this overload delegates to it.</para>
        /// </remarks>
        public IServiceCollection AddSqliteBudgetLedger(SqliteBudgetLedgerTarget target, Action<SqliteBudgetLedgerOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            var options = new SqliteBudgetLedgerOptions();
            configure?.Invoke(options);
            var settings = new SqliteBudgetLedgerSettings(
                options.LockTimeout,
                options.MaximumPayloadBytes,
                options.MaximumResultBytes,
                options.MaximumBatchSize,
                options.MaximumLimitsPerScope,
                options.MaximumLineageDepth);
            return services.AddSqliteBudgetLedger(target, settings);
        }

        /// <summary>Adds one explicitly configured singleton SQLite ledger without opening its target.</summary>
        /// <param name="target">The immutable fixed bootstrap target.</param>
        /// <param name="settings">The immutable transaction and evidence bounds.</param>
        /// <returns>The same caller-owned collection.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">This leaf already captured different target or settings evidence.</exception>
        /// <remarks>Exact repeated registration is idempotent. Other <see cref="IBudgetLedger"/> registrations remain visible for composition validation. The caller separately selects the budget runtime and dimension catalog and invokes <see cref="SqliteBudgetLedger.InitializeAsync"/> during trusted bootstrap.</remarks>
        public IServiceCollection AddSqliteBudgetLedger(SqliteBudgetLedgerTarget target, SqliteBudgetLedgerSettings settings)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(settings);
            var existingTarget = GetCaptured<SqliteBudgetLedgerTarget>(services);
            var existingSettings = GetCaptured<SqliteBudgetLedgerSettings>(services);
            if (existingTarget is not null && existingTarget != target)
            {
                throw new InvalidOperationException("The SQLite budget-ledger leaf is already configured for a different target.");
            }
            if (existingSettings is not null && existingSettings != settings)
            {
                throw new InvalidOperationException("The SQLite budget-ledger leaf is already configured with different settings.");
            }
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<BudgetScopeId>, GuidBudgetScopeIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<BudgetReservationId>, GuidBudgetReservationIdGenerator>();
            if (existingTarget is null)
            {
                _ = services.AddSingleton(target);
            }
            if (existingSettings is null)
            {
                _ = services.AddSingleton(settings);
            }
            if (!services.Any(static descriptor => descriptor.ServiceType == typeof(IBudgetLedger)
                    && descriptor.Lifetime == ServiceLifetime.Singleton
                    && descriptor.ImplementationType == typeof(SqliteBudgetLedger)))
            {
                services.Add(ServiceDescriptor.Singleton<IBudgetLedger, SqliteBudgetLedger>());
            }
            return services;

            static T? GetCaptured<T>(IServiceCollection source) where T : class
            {
                var matches = source.Where(static descriptor => descriptor.ServiceType == typeof(T) && descriptor.ServiceKey is null).ToArray();
                return matches.Length switch
                {
                    0 => null,
                    1 when matches[0].ImplementationInstance is T value => value,
                    _ => throw new InvalidOperationException($"The SQLite budget-ledger {typeof(T).Name} registration is not one exact captured instance."),
                };
            }
        }
    }
}
