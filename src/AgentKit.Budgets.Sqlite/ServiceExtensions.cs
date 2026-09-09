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
