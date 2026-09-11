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
        /// <summary>Registers the replaceable default catalog collision policy and its immutable merge coordinator.</summary>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Idempotent default registration preserves host policy and clock choices, adds safe logging, and activates no service. It discovers no source and does not replace the legacy tool catalog.</remarks>
        public IServiceCollection AddToolCatalogMerging()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IToolCatalogMergePolicy, RejectingToolCatalogMergePolicy>();
            services.TryAddSingleton(static provider =>
            {
                var policies = provider.GetServices<IToolCatalogMergePolicy>().ToArray();
                return policies.Length == 1
                    ? new ToolCatalogMerger(policies[0], provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<ToolCatalogMerger>>())
                    : throw new InvalidOperationException("Catalog merging requires exactly one unkeyed merge policy.");
            });
            return services;
        }

        /// <summary>Explicitly replaces the process-level policy for complete catalog collision decisions.</summary>
        /// <typeparam name="TPolicy">The concurrently callable policy selecting only explicitly configured captured evidence.</typeparam>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Removes all unkeyed policy registrations and adds one singleton without activation. Keyed host registrations and previously constructed hosts remain unchanged. Later default registration preserves this choice.</remarks>
        public IServiceCollection ReplaceToolCatalogMergePolicy<TPolicy>() where TPolicy : class, IToolCatalogMergePolicy
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddToolCatalogMerging();
            foreach (var descriptor in services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolCatalogMergePolicy)).ToArray())
            {
                _ = services.Remove(descriptor);
            }
            _ = services.AddSingleton<IToolCatalogMergePolicy, TPolicy>();
            return services;
        }

        /// <summary>Registers an immutable application tool source under its exact typed source key.</summary>
        /// <param name="snapshot">The nonnull explicit source publication, including its source version.</param>
        /// <param name="invokers">The complete nonnull borrowed binding map for the publication; the host owner keeps every instance alive through all captures and leases.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="snapshot"/>, <paramref name="invokers"/>, or a binding is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A binding identity is default.</exception>
        /// <exception cref="ArgumentException">Bindings do not exactly match the publication, or this source already has a keyed provider registration.</exception>
        /// <remarks>
        /// Adds one keyed singleton <see cref="IToolProvider"/> using <see cref="ToolSourceId"/> itself
        /// as the service key. Duplicate source registration rejects before mutation, even for identical
        /// content; use explicit replacement to change it. Captures are created per discovery. Logging
        /// and a default clock are added without replacing host choices. This method builds no provider,
        /// activates no service, registers no unkeyed fallback, and never takes ownership of invokers.
        /// Catalog selection and model exposure remain separate from source registration.
        /// </remarks>
        public IServiceCollection AddStaticToolProvider(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(invokers);
            var bindings = new ToolProviderBindings(snapshot, invokers);
            return ToolServiceRegistration.RegisterStaticProvider(services, bindings, false);
        }

        /// <summary>Explicitly replaces the configured provider for one exact source key before a host is built.</summary>
        /// <param name="snapshot">The nonnull replacement source publication with its explicit version.</param>
        /// <param name="invokers">The complete borrowed binding map; previously captured bindings keep their original owners.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="snapshot"/>, <paramref name="invokers"/>, or a binding is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A binding identity is default.</exception>
        /// <exception cref="ArgumentException">Bindings do not exactly match the supplied publication.</exception>
        /// <remarks>
        /// Removes only keyed <see cref="IToolProvider"/> registrations with this exact typed source
        /// identity, including opaque factories, without activation. Other sources and unkeyed host
        /// registrations remain intact. Missing sources are added. Existing hosts, providers, captures,
        /// and leases are unchanged; replacement neither disposes nor reclaims their borrowed instances.
        /// Invalid input rejects before mutation. No host, service, or source is activated here.
        /// </remarks>
        public IServiceCollection ReplaceStaticToolProvider(ToolProviderSnapshot snapshot, ImmutableDictionary<ToolIdentity, IToolInvoker> invokers)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(invokers);
            var bindings = new ToolProviderBindings(snapshot, invokers);
            return ToolServiceRegistration.RegisterStaticProvider(services, bindings, true);
        }

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
