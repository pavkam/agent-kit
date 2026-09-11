// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the tool runtime and retained projection policies.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the built-in tool catalog, allow-list authorizer, and
        /// invoker, together with the exact-version projection-policy catalog.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="AgentToolsOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c>
        /// semantics, so calling this more than once keeps the first
        /// registration. This method does not register any concrete
        /// <see cref="ITool"/>; use <see cref="AddTool{TTool}"/> to add
        /// each tool the application wants available.
        /// The catalog receives only explicitly registered policy snapshots and
        /// preserves a host clock, supplying <see cref="TimeProvider.System"/>
        /// only when no clock is registered.
        /// </remarks>
        public IServiceCollection AddAgentTools(Action<AgentToolsOptions>? configure = null)
        {
            _ = services.AddToolResultProjectionPolicyCatalog();
            var optionsBuilder = services.AddOptions<AgentToolsOptions>();
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton<IToolAuthorizer, AllowListToolAuthorizer>();
            services.TryAddSingleton<IToolCatalog>(static provider => new ToolCatalog(provider.GetServices<ITool>()));
            services.TryAddSingleton<IToolInvoker, DefaultToolInvoker>();

            return services;
        }

        /// <summary>Registers the replaceable exact-version projection-policy catalog over host-supplied immutable snapshots.</summary>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Repeated calls preserve the first catalog and clock registration. Logging is additive. No policy content
        /// or persistence adapter is invented; an empty catalog returns unavailable. Registration does not build a provider.
        /// </remarks>
        public IServiceCollection AddToolResultProjectionPolicyCatalog()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IToolResultProjectionPolicyCatalog, ToolResultProjectionPolicyCatalog>();
            return services;
        }

        /// <summary>Adds a retained immutable projection-policy revision and its replaceable catalog registration.</summary>
        /// <param name="snapshot">The nonnull policy content under its exact published key and revision.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="snapshot"/> is null.</exception>
        /// <remarks>
        /// Snapshots are additive. The first-party catalog captures them when constructed, accepts equivalent duplicates,
        /// and rejects conflicting content under one reference at composition resolution. Hosts select every retained
        /// revision explicitly. Later changes to the service collection do not alter an already constructed catalog.
        /// </remarks>
        public IServiceCollection AddToolResultProjectionPolicy(ToolResultProjectionPolicySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(snapshot);
            _ = services.AddToolResultProjectionPolicyCatalog();
            _ = services.AddSingleton(snapshot);
            return services;
        }

        /// <summary>Replaces the configured snapshot instances for one exact policy reference before catalog capture.</summary>
        /// <param name="snapshot">The nonnull replacement content under the exact reference to replace.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="snapshot"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="services"/> contains an unkeyed policy snapshot registration whose reference cannot be inspected without activation.</exception>
        /// <remarks>
        /// Removes every unkeyed snapshot instance with the same key and revision, preserves other revisions,
        /// and adds the replacement. Opaque factory or type registrations reject before any mutation; this method
        /// never activates services to discover their reference. Already constructed catalogs remain unchanged.
        /// Hosts must not replace retained content needed to recover an authoritative result under its original reference.
        /// </remarks>
        public IServiceCollection ReplaceToolResultProjectionPolicy(ToolResultProjectionPolicySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(snapshot);
            var registrations = services.Where(static descriptor => !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(ToolResultProjectionPolicySnapshot)).ToArray();
            ArgumentException.ThrowIfNotEqual(registrations.All(static descriptor =>
                descriptor.ImplementationInstance is ToolResultProjectionPolicySnapshot), true, nameof(services));
            foreach (var descriptor in registrations)
            {
                if (((ToolResultProjectionPolicySnapshot) descriptor.ImplementationInstance!).Reference == snapshot.Reference)
                {
                    _ = services.Remove(descriptor);
                }
            }

            return services.AddToolResultProjectionPolicy(snapshot);
        }

        /// <summary>Explicitly replaces the singular projection-policy catalog implementation.</summary>
        /// <typeparam name="TCatalog">The singleton, concurrently callable catalog supplied by the host.</typeparam>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Removes all existing registrations of the catalog contract and registers the replacement once.
        /// Retained policy snapshots, host clocks, and logging registrations are preserved. No provider is built
        /// and no catalog is activated; later default registration does not replace this explicit choice.
        /// </remarks>
        public IServiceCollection ReplaceToolResultProjectionPolicyCatalog<TCatalog>()
            where TCatalog : class, IToolResultProjectionPolicyCatalog
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddToolResultProjectionPolicyCatalog();
            _ = services.RemoveAll<IToolResultProjectionPolicyCatalog>();
            _ = services.AddSingleton<IToolResultProjectionPolicyCatalog, TCatalog>();
            return services;
        }

        /// <summary>Adds <typeparamref name="TTool"/> to the additive set of registered tools.</summary>
        /// <typeparam name="TTool">The tool implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection AddTool<TTool>()
            where TTool : class, ITool
        {
            _ = services.AddSingleton<ITool, TTool>();
            return services;
        }
    }
}
