// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite;

/// <summary>Provides explicit dependency-injection selection for host-local SQLite durable-execution adapters.</summary>
/// <remarks>
/// The durability runtime remains a separate registration; this leaf supplies only lease coordination and journal
/// storage. Every registration here is keyed, because a durability profile selects its journal and lease manager by
/// exact key and registration order must never choose a persistence target. The database target is an external fact
/// the host supplies; nothing here fabricates one.
/// </remarks>
public static class ServiceExtensions
{
    /// <summary>Adds SQLite durable-execution adapter registration operations to a caller-owned service collection.</summary>
    /// <param name="services">The mutable application composition receiving the explicit adapter selection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one keyed singleton host-local durable backend coordinated through a shared SQLite database.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this backend.</param>
        /// <param name="supportedOperations">The operation names the backend may own, or the default empty array to place no restriction.</param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text, or <paramref name="supportedOperations"/> contains a blank or duplicate name.</exception>
        /// <remarks>
        /// Repeating this exact keyed registration is idempotent, and a backend registered under a different key is
        /// neither replaced nor hidden. The backend advertises no reconciliation, so a profile that selects it resolves
        /// an unknown effect through operator action rather than an optimistic retry.
        /// </remarks>
        public IServiceCollection AddSqliteDurableExecutionBackend(
            DurableBackendKey key,
            ImmutableArray<DurableOperationName> supportedOperations = default)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            services.TryAddKeyedSingleton<IDurableExecutionBackend>(
                key.Value, (_, _) => new SqliteDurableExecutionBackend(key, supportedOperations));
            return services;
        }

        /// <summary>Adds one keyed singleton SQLite lease manager over an explicitly supplied database.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this lease manager.</param>
        /// <param name="database">The non-null shared database boundary this manager and its journal must both commit against.</param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="database"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text.</exception>
        /// <remarks>
        /// Repeating this exact keyed registration is idempotent, and a lease manager registered under a different key
        /// is neither replaced nor hidden. An existing clock or lease-identity generator is preserved so a host may
        /// replace either. Pass the same <see cref="SqliteDurableDatabase"/> instance to
        /// <c>AddSqliteDurableOperationJournal</c>: a fencing token only fences a journal write when both are allocated
        /// and enforced inside one atomic store. The store must be initialized during trusted host bootstrap before
        /// any lease is acquired.
        /// </remarks>
        public IServiceCollection AddSqliteDurableLeaseManager(
            DurableLeaseManagerKey key,
            SqliteDurableDatabase database)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(database);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<ExecutionLeaseId>, GuidExecutionLeaseIdGenerator>();
            services.TryAddKeyedSingleton<IDurableLeaseManager>(key.Value, (provider, _) =>
                new SqliteDurableLeaseManager(
                    database,
                    provider.GetRequiredService<IIdentifierGenerator<ExecutionLeaseId>>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<SqliteDurableLeaseManager>>()));
            return services;
        }

        /// <summary>Adds one keyed singleton SQLite durable journal over an explicitly supplied database.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this journal; the journal also requires it in every authorized request.</param>
        /// <param name="database">The non-null shared database boundary this journal and its lease manager must both commit against.</param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="database"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text.</exception>
        /// <remarks>
        /// Repeating this exact keyed registration is idempotent, and a journal registered under a different key is
        /// neither replaced nor hidden. The registration does not create a grant store or audit dispatcher: journal
        /// access is protected, so composition must supply <see cref="ISecurityGrantStore"/> and
        /// <see cref="ISecurityAuditDispatcher"/> before the journal resolves. The store must be initialized during
        /// trusted host bootstrap before any operation is journaled.
        /// </remarks>
        public IServiceCollection AddSqliteDurableOperationJournal(
            DurableJournalKey key,
            SqliteDurableDatabase database)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(database);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
            services.TryAddKeyedSingleton<IDurableOperationJournal>(key.Value, (provider, _) =>
                new SqliteDurableOperationJournal(
                    key,
                    database,
                    provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                    provider.GetRequiredService<ISecurityAuditDispatcher>(),
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<SqliteDurableOperationJournal>>()));
            return services;
        }
    }
}
