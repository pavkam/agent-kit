// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for session coordination.
/// </summary>
/// <remarks>
/// The package registers singular coordinator, run-coordinator, directory,
/// catalog, and selector axes. Store implementations are additive and selected
/// by their immutable descriptors and exact keys. The run's captured session
/// profile chooses among that frozen store set; registration order never acts
/// as an implicit default-store selection.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the default session coordinator and run coordinator.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="AgentSessionOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Singular runtime defaults use <c>TryAdd</c> semantics, so hosts may
        /// replace them before or after this call. Repeated configuration
        /// delegates remain additive under the options pattern. This method
        /// registers routing services but no concrete <see cref="ISessionStore"/>;
        /// each selected store must be added explicitly through
        /// <see cref="AddSessionStore{TStore}"/> or a leaf package.
        /// </remarks>
        public IServiceCollection AddAgentSession(Action<AgentSessionOptions>? configure = null)
        {
            _ = services.AddAgentKitObservability();
            var optionsBuilder = services.AddOptions<AgentSessionOptions>()
                .Validate(o => o.MaximumAppendEntries > 0, "MaximumAppendEntries must be positive.")
                .Validate(o => o.MaximumPageSize > 0, "MaximumPageSize must be positive.")
                .Validate(o => o.SecurityRequestLifetime > TimeSpan.Zero,
                    "SecurityRequestLifetime must be positive.")
                .Validate(o => o.SecurityRequestLifetime <= TimeSpan.FromHours(1),
                    "SecurityRequestLifetime must not exceed one hour.")
                .Validate(o => o.BusyWaitTimeout >= TimeSpan.Zero, "BusyWaitTimeout must not be negative.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SessionLeaseId>>(
                _ => new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SessionId>>(
                _ => new GuidIdentifierGenerator<SessionId>(static value => new SessionId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(
                _ => new GuidIdentifierGenerator<SecurityEnforcementIntentId>(
                    static value => new SecurityEnforcementIntentId(value)));
            services.TryAddSingleton<ISessionRetentionPolicy, NeverRetireSessionRetentionPolicy>();
            services.TryAddSingleton<SessionStoreBindingSnapshot>();
            services.TryAddSingleton<ISessionStoreCatalog>(provider =>
                new DefaultSessionStoreCatalog(provider.GetRequiredService<SessionStoreBindingSnapshot>()));
            services.TryAddSingleton<ISessionStoreSelector>(provider =>
                new DefaultSessionStoreSelector(
                    provider.GetRequiredService<SessionStoreBindingSnapshot>(),
                    provider.GetRequiredService<ILogger<DefaultSessionStoreSelector>>()));
            services.TryAddSingleton<ISessionCoordinator, DefaultSessionCoordinator>();
            services.TryAddSingleton<ISessionRunCoordinator, DefaultSessionRunCoordinator>();

            return services;
        }

        /// <summary>
        /// Adds <typeparamref name="TStore"/> to the explicitly composed session-store set.
        /// </summary>
        /// <typeparam name="TStore">The store implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Registrations are additive, including repeated registrations of the
        /// same implementation type. The immutable binding snapshot rejects
        /// different store instances that publish the same descriptor key,
        /// preventing ambiguous exact-key selection.
        /// </remarks>
        public IServiceCollection AddSessionStore<TStore>()
            where TStore : class, ISessionStore
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddSingleton<ISessionStore, TStore>();
            return services;
        }

        /// <summary>
        /// Adds <typeparamref name="TSink"/> to the additive, ordered set of
        /// session event sinks.
        /// </summary>
        /// <typeparam name="TSink">The event sink implementation to add.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddSessionEventSink<TSink>()
            where TSink : class, ISessionEventSink
        {
            _ = services.AddSingleton<ISessionEventSink, TSink>();
            return services;
        }

        /// <summary>
        /// Replaces the singular <see cref="ISessionRetentionPolicy"/> with
        /// <typeparamref name="TPolicy"/>, removing the documented
        /// keep-forever default.
        /// </summary>
        /// <typeparam name="TPolicy">The retention policy implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceSessionRetentionPolicy<TPolicy>()
            where TPolicy : class, ISessionRetentionPolicy
        {
            _ = services.RemoveAll<ISessionRetentionPolicy>();
            _ = services.AddSingleton<ISessionRetentionPolicy, TPolicy>();
            return services;
        }
    }
}
