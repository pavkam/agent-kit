// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for session coordination.
/// </summary>
/// <remarks>
/// This is a reduced registration surface compared to the full sessions
/// architecture: it registers one singular <see cref="ISessionCoordinator"/>
/// and <see cref="ISessionRunCoordinator"/> rather than a keyed
/// per-agent-definition selection, and one directly injected
/// <see cref="ISessionStore"/> rather than a multi-store directory and
/// selector. Keyed, multi-agent selection arrives with the AgentKit facade;
/// this shape is what a standalone application or test host needs today.
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
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once keeps the first registration.
        /// This method does not register an <see cref="ISessionStore"/>;
        /// composition is incomplete until <c>AddSessionStore</c> (or an
        /// equivalent leaf package registration) supplies one.
        /// </remarks>
        public IServiceCollection AddAgentSession(Action<AgentSessionOptions>? configure = null)
        {
            var optionsBuilder = services.AddOptions<AgentSessionOptions>()
                .Validate(o => o.MaximumAppendEntries > 0, "MaximumAppendEntries must be positive.")
                .Validate(o => o.MaximumPageSize > 0, "MaximumPageSize must be positive.")
                .Validate(o => o.BusyWaitTimeout >= TimeSpan.Zero, "BusyWaitTimeout must not be negative.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SessionLeaseId>>(
                _ => new GuidIdentifierGenerator<SessionLeaseId>(static value => new SessionLeaseId(value)));
            services.TryAddSingleton<ISessionRetentionPolicy, NeverRetireSessionRetentionPolicy>();
            services.TryAddSingleton<ISessionCoordinator, DefaultSessionCoordinator>();
            services.TryAddSingleton<ISessionRunCoordinator, DefaultSessionRunCoordinator>();

            return services;
        }

        /// <summary>
        /// Registers <typeparamref name="TStore"/> as the singular session
        /// store used by the default coordinator, unless a store is already
        /// registered.
        /// </summary>
        /// <typeparam name="TStore">The store implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddSessionStore<TStore>()
            where TStore : class, ISessionStore
        {
            services.TryAddSingleton<ISessionStore, TStore>();
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
