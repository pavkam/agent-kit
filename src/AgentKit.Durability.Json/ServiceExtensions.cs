// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json;

/// <summary>Provides explicit dependency-injection selection for the durable inspectable JSON operation journal.</summary>
/// <remarks>
/// The durability runtime remains a separate registration; this leaf supplies only journal storage. There is no JSON
/// lease manager: the adapter holds an advisory exclusive lock and rejects a second writer, so it cannot honestly
/// coordinate ownership between processes. A composition that needs cross-process ownership selects the SQLite lease
/// manager or a distributed backend. The store root is an external fact the host supplies; nothing here fabricates one.
/// </remarks>
public static class ServiceExtensions
{
    /// <summary>Adds JSON durable-journal registration operations to a caller-owned service collection.</summary>
    /// <param name="services">The mutable application composition receiving the explicit adapter selection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>Adds one keyed singleton durable JSON operation journal over an explicitly supplied store root.</summary>
        /// <param name="key">The exact nonblank key a durability profile uses to select this journal; the journal also requires it in every authorized request.</param>
        /// <param name="target">The non-null exact store root and bootstrap effects supplied by the host.</param>
        /// <param name="configure">An optional delegate that adjusts the evidence bounds, compaction policy, and encoding contract before they are validated and frozen.</param>
        /// <returns>The same caller-owned service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> carries no key text.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured bound or the compaction threshold is not positive.</exception>
        /// <remarks>
        /// Settings are materialized and validated during registration, not on first use, so an impossible bound fails
        /// at composition rather than in the middle of a run. Repeating this exact keyed registration is idempotent,
        /// and a journal registered under a different key is neither replaced nor hidden. The registration does not
        /// create a grant store or audit dispatcher: journal access is protected, so composition must supply
        /// <see cref="ISecurityGrantStore"/> and <see cref="ISecurityAuditDispatcher"/> before the journal resolves.
        /// The journal must be initialized during trusted host bootstrap before any operation is journaled.
        /// </remarks>
        public IServiceCollection AddJsonDurableOperationJournal(
            DurableJournalKey key,
            JsonDurableStoreTarget target,
            Action<JsonDurableStoreOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
            var options = new JsonDurableStoreOptions();
            configure?.Invoke(options);
            var settings = new JsonDurableStoreSettings(
                options.MaximumRecordBytes,
                options.MaximumDocumentBytes,
                options.CompactionRecordThreshold,
                options.Encoding);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SecurityAuditRecordId>, GuidSecurityAuditRecordIdGenerator>();
            services.TryAddKeyedSingleton<IDurableOperationJournal>(key.Value, (provider, _) =>
                new JsonDurableOperationJournal(
                    key,
                    target,
                    settings,
                    provider.GetRequiredService<IIdentifierGenerator<SecurityAuditRecordId>>(),
                    provider.GetRequiredService<ISecurityAuditDispatcher>(),
                    provider.GetRequiredService<ISecurityGrantStore>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<ILogger<JsonDurableOperationJournal>>()));
            return services;
        }
    }
}
