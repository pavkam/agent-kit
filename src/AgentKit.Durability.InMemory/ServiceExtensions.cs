// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Provides explicit dependency-injection selection for process-local durable-execution adapters.</summary>
/// <remarks>
/// The durability runtime remains a separate registration; these leaves supply only process-local lease coordination
/// and journal storage. Every registration here is keyed, because a durability profile selects its journal and lease
/// manager by exact key and registration order must never choose a persistence target.
/// </remarks>
public static class ServiceExtensions
{
    /// <summary>Adds durable-execution adapter registration operations to a caller-owned service collection.</summary>
    /// <param name="services">The mutable application composition receiving the explicit adapter selection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one keyed singleton process-local durable backend that keeps operation ownership in this process.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this backend.</param>
        /// <param name="supportedOperations">
        /// The operation names the backend may own, or the default empty array to place no restriction.
        /// </param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text, or <paramref name="supportedOperations"/> contains a blank or duplicate name.</exception>
        /// <remarks>
        /// Repeating this exact keyed registration is idempotent, and a backend registered under a different key is
        /// neither replaced nor hidden. The backend advertises no reconciliation, so a profile that selects it resolves
        /// an unknown effect through operator action rather than an optimistic retry.
        /// </remarks>
        public IServiceCollection AddInMemoryDurableExecutionBackend(
            DurableBackendKey key,
            ImmutableArray<DurableOperationName> supportedOperations = default)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            services.TryAddKeyedSingleton<IDurableExecutionBackend>(
                key.Value, (_, _) => new InMemoryDurableExecutionBackend(key, supportedOperations));
            return services;
        }

        /// <summary>Adds one keyed singleton process-local lease manager and its replaceable clock and identity generator.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this lease manager.</param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null, or <paramref name="key"/> carries no key text.</exception>
        /// <remarks>
        /// Repeating this exact keyed registration is idempotent, and a lease manager registered under a different key
        /// is neither replaced nor hidden. An existing clock or lease-identity generator is preserved so a host may
        /// replace either. This adapter advertises no cross-process ownership: its fencing tokens are authoritative
        /// only within this process.
        /// </remarks>
        public IServiceCollection AddInMemoryDurableLeaseManager(DurableLeaseManagerKey key)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<ExecutionLeaseId>, GuidExecutionLeaseIdGenerator>();
            services.TryAddKeyedSingleton<IDurableLeaseManager>(key.Value, (provider, _) =>
                new InMemoryDurableLeaseManager(
                    provider.GetRequiredService<IIdentifierGenerator<ExecutionLeaseId>>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<InMemoryDurableLeaseManager>>()));
            return services;
        }

        /// <summary>Adds one keyed singleton process-local durable journal and its replaceable clock and audit-identity generator.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this journal; the journal also requires it in every authorized request.</param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null, or <paramref name="key"/> carries no key text.</exception>
        /// <remarks>
        /// Repeating this exact keyed registration is idempotent, and a journal registered under a different key is
        /// neither replaced nor hidden. The registration does not create a grant store or audit dispatcher: journal
        /// access is protected, so composition must supply <see cref="ISecurityGrantStore"/> and
        /// <see cref="ISecurityAuditDispatcher"/> before the journal resolves. This adapter is explicitly ephemeral and
        /// claims no crash recovery.
        /// </remarks>
        public IServiceCollection AddInMemoryDurableOperationJournal(DurableJournalKey key)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
            services.TryAddKeyedSingleton<IDurableOperationJournal>(key.Value, (provider, _) =>
                new InMemoryDurableOperationJournal(
                    key,
                    provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                    provider.GetRequiredService<ISecurityAuditDispatcher>(),
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<InMemoryDurableOperationJournal>>()));
            return services;
        }
    }
}
