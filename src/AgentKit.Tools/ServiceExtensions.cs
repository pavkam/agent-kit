// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using AgentKit.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>Dependency-injection registration for the tool runtime and retained projection policies.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the replaceable bounded provider-neutral presenter over additive exact-descriptor formatters.</summary>
        /// <returns>The same collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>The singleton captures formatters once when activated. It performs no catalog lookup, authorization, invocation, or model-history projection.</remarks>
        public IServiceCollection AddToolPresentation()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IToolPresenter, ToolPresenter>();
            return services;
        }

        /// <summary>Registers the replaceable bounded canonical tool-schema compiler and its content-free diagnostics.</summary>
        /// <returns>The same service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Idempotently preserves an existing unkeyed engine and host clock/logger choices. No provider translation, tool exposure, storage, or authority is registered. The engine and its immutable compiled handles are concurrently callable; limits are supplied explicitly per operation.</remarks>
        public IServiceCollection AddToolSchemaEngine()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IToolSchemaEngine>(provider => new BoundedToolSchemaEngine(provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<BoundedToolSchemaEngine>>(), provider.GetRequiredService<ILogger<CompiledToolSchema>>()));
            return services;
        }

        /// <summary>Explicitly replaces every unkeyed canonical tool-schema compiler registration.</summary>
        /// <typeparam name="TEngine">A concurrently callable engine with an immutable declared profile and complete bounded preflight.</typeparam>
        /// <returns>The same service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Activates no service and preserves keyed engines and existing hosts or compiled handles. Replacement selects future compositions without redirecting retained canonical schemas.</remarks>
        public IServiceCollection ReplaceToolSchemaEngine<TEngine>() where TEngine : class, IToolSchemaEngine
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddToolSchemaEngine();
            foreach (var descriptor in services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolSchemaEngine)).ToArray()) { _ = services.Remove(descriptor); }
            _ = services.AddSingleton<IToolSchemaEngine, TEngine>();
            return services;
        }

        /// <summary>Explicitly replaces the process-level <see cref="IToolScheduler"/> registration.</summary>
        /// <typeparam name="TScheduler">A scheduler that executes prepared batches under deterministic ordering guarantees.</typeparam>
        /// <returns>The same service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Removes every unkeyed scheduler descriptor without activation. Keyed schedulers and retained batches remain unchanged.</remarks>
        public IServiceCollection ReplaceToolScheduler<TScheduler>() where TScheduler : class, IToolScheduler
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddOptions<ToolRuntimeOptions>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IToolResultNormalizer, ToolResultNormalizer>();
            foreach (var descriptor in services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolScheduler)).ToArray())
            {
                _ = services.Remove(descriptor);
            }

            _ = services.AddSingleton<IToolScheduler, TScheduler>();
            return services;
        }

        /// <summary>Explicitly replaces the process-level unkeyed <see cref="IToolExecutor"/> registration.</summary>
        /// <typeparam name="TExecutor">The spec-shaped executor implementation to register.</typeparam>
        /// <returns>The same service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Removes every unkeyed executor descriptor without activation. Keyed executors and in-flight runs remain
        /// unchanged. Call after <see cref="AddAgentTools"/> to opt into the new runtime while legacy types remain
        /// registered for bridge scenarios. Spec-shaped executors require <see cref="ISecurityAuthoritySelector"/>
        /// from the permissions stack; <see cref="AddAgentTools"/> does not register security authority.
        /// </remarks>
        public IServiceCollection ReplaceToolExecutor<TExecutor>() where TExecutor : class, IToolExecutor
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentTools();
            foreach (var descriptor in services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolExecutor)).ToArray())
            {
                _ = services.Remove(descriptor);
            }

            _ = services.AddSingleton<IToolExecutor, TExecutor>();
            return services;
        }

        /// <summary>Explicitly replaces one keyed <see cref="IToolExecutor"/> registration.</summary>
        /// <typeparam name="TExecutor">The executor implementation bound to <paramref name="executorKey"/>.</typeparam>
        /// <param name="executorKey">The nondefault executor component key.</param>
        /// <returns>The same service collection for continued composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="executorKey"/> is default.</exception>
        public IServiceCollection ReplaceToolExecutor<TExecutor>(ComponentKey<IToolExecutor> executorKey)
            where TExecutor : class, IToolExecutor
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentOutOfRangeException.ThrowIfEqual(executorKey, default);
            _ = services.AddAgentTools();
            foreach (var descriptor in services.Where(descriptor =>
                         descriptor.IsKeyedService
                         && descriptor.ServiceType == typeof(IToolExecutor)
                         && executorKey.Value.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal))
                     .ToArray())
            {
                _ = services.Remove(descriptor);
            }

            _ = services.AddKeyedSingleton<IToolExecutor, TExecutor>(executorKey.Value);
            return services;
        }

        /// <summary>Adds one application-scoped tool invoker under the shared application tool source.</summary>
        /// <typeparam name="TInvoker">The invoker implementation registered for one descriptor identity.</typeparam>
        /// <param name="descriptor">The complete immutable descriptor published for discovery and merge.</param>
        /// <param name="lifetime">The service lifetime for <typeparamref name="TInvoker"/>.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The descriptor identity or version is default.</exception>
        /// <remarks>
        /// Registers a keyed <see cref="IToolInvoker"/> for the descriptor identity, records composition metadata for
        /// <see cref="ApplicationToolProvider"/>, and ensures that provider is registered once. Repeated registration
        /// for the same identity rejects before mutation.
        /// </remarks>
        public IServiceCollection AddToolInvoker<TInvoker>(ToolDescriptor descriptor, ServiceLifetime lifetime = ServiceLifetime.Scoped)
            where TInvoker : class, IToolInvoker
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentOutOfRangeException.ThrowIfEqual(descriptor.Id, default);
            ArgumentOutOfRangeException.ThrowIfEqual(descriptor.Version, default);
            var identity = new ToolIdentity(descriptor.Id, descriptor.Version);
            var duplicate = services.Any(descriptorEntry =>
                !descriptorEntry.IsKeyedService
                && descriptorEntry.ServiceType == typeof(RegisteredToolInvoker)
                && descriptorEntry.ImplementationInstance is RegisteredToolInvoker marker
                && marker.Identity == identity);
            ArgumentException.ThrowIfNotEqual(duplicate, false, nameof(descriptor));
            _ = lifetime switch
            {
                ServiceLifetime.Singleton => services.AddSingleton<TInvoker>(),
                ServiceLifetime.Scoped => services.AddScoped<TInvoker>(),
                ServiceLifetime.Transient => services.AddTransient<TInvoker>(),
                _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Only Singleton, Scoped, and Transient are supported."),
            };
            _ = services.AddKeyedSingleton<IToolInvoker>(
                identity,
                (provider, _) => provider.GetRequiredService<TInvoker>());
            _ = services.AddSingleton(new RegisteredToolInvoker(descriptor));
            return ToolServiceRegistration.EnsureApplicationToolProvider(services);
        }

        /// <summary>Registers the replaceable materialized catalog of explicitly published toolsets and source providers.</summary>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Idempotent default registration preserves host catalog, clock, and logging choices. It activates no service, creates no default toolset, and registers no persistence adapter. Source and publication cardinality is validated when the catalog is materialized. The internal discovery coordinator requires exactly one materialized view and retains source acquisitions through later merge/preflight; it does not publish model-facing tools.</remarks>
        public IServiceCollection AddToolRegistrationCatalog()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(ToolServiceRegistration.CreateRegistrationCatalog);
            services.TryAddSingleton(provider =>
            {
                var catalogs = provider.GetServices<IToolRegistrationCatalog>().ToArray();
                return catalogs.Length == 1 && catalogs[0] is { } catalog
                    ? new ToolCatalogDiscovery(catalog, provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ILogger<ToolCatalogDiscovery>>(),
                        provider.GetRequiredService<ILogger<ToolDiscoveryCapture>>(), provider.GetRequiredService<ILogger<ToolCatalogCapture>>())
                    : throw new InvalidOperationException("Tool discovery requires exactly one materialized registration catalog.");
            });
            return services;
        }

        /// <summary>Explicitly replaces the process-level materialized tool registration catalog.</summary>
        /// <typeparam name="TCatalog">The concurrently callable catalog preserving complete authored selection and borrowed provider ownership.</typeparam>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Replaces all unkeyed catalog descriptors without activation. Keyed host catalogs, publications, source providers, clocks, and previously constructed hosts remain unchanged.</remarks>
        public IServiceCollection ReplaceToolRegistrationCatalog<TCatalog>() where TCatalog : class, IToolRegistrationCatalog
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddToolRegistrationCatalog();
            foreach (var descriptor in services.Where(static descriptor => !descriptor.IsKeyedService && descriptor.ServiceType == typeof(IToolRegistrationCatalog)).ToArray()) { _ = services.Remove(descriptor); }
            _ = services.AddSingleton<IToolRegistrationCatalog, TCatalog>();
            return services;
        }

        /// <summary>Publishes an explicit immutable toolset under its exact typed family key.</summary>
        /// <param name="publication">The nonnull publication, including real versions, source membership, policy family, and explicit aliases.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="publication"/> is null.</exception>
        /// <exception cref="ArgumentException">The exact key already exists, or internal registration metadata cannot be inspected without activation.</exception>
        /// <remarks>Duplicate keys reject even for equal publications. No provider is activated and no alias or version is inferred. Sources may be registered before or after this publication; complete membership is validated at catalog construction.</remarks>
        public IServiceCollection AddToolset(ToolsetPublication publication)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(publication);
            return ToolServiceRegistration.RegisterToolset(services, publication, false);
        }

        /// <summary>Explicitly replaces the publication for one exact toolset key before a host is composed.</summary>
        /// <param name="publication">The nonnull complete replacement publication.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="publication"/> is null.</exception>
        /// <exception cref="ArgumentException">Internal registration metadata cannot be inspected without activation.</exception>
        /// <remarks>Removes every matching typed-key publication descriptor, including opaque factories, without activation. Other keys, foreign-key registrations, old hosts, and existing discovery selections remain intact. An absent key is added.</remarks>
        public IServiceCollection ReplaceToolset(ToolsetPublication publication)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(publication);
            return ToolServiceRegistration.RegisterToolset(services, publication, true);
        }

        /// <summary>Registers an explicitly keyed, concurrently callable discovery provider.</summary>
        /// <typeparam name="TProvider">The singleton provider whose stable source identity must match the key.</typeparam>
        /// <param name="sourceId">The nondefault exact source key, available through the standard DI service-key parameter attribute.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
        /// <exception cref="ArgumentException">The exact source key exists or internal metadata cannot be inspected without activation.</exception>
        /// <remarks>Registers no unkeyed fallback and activates no provider. The host owns the singleton; independent source captures own per-discovery resources. Catalog materialization validates the actual provider identity before any discovery.</remarks>
        public IServiceCollection AddToolProvider<TProvider>(ToolSourceId sourceId) where TProvider : class, IToolProvider
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
            return ToolServiceRegistration.RegisterProvider(services, sourceId, ServiceDescriptor.KeyedSingleton<IToolProvider, TProvider>(sourceId), false);
        }

        /// <summary>Registers an existing borrowed discovery provider under its exact typed source key.</summary>
        /// <param name="sourceId">The nondefault exact key matching the provider's stable identity.</param>
        /// <param name="provider">The nonnull existing provider, kept alive by its original owner through all captures and leases.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
        /// <exception cref="ArgumentException">Provider identity differs, the key exists, or registration metadata is malformed.</exception>
        /// <remarks>Validates source metadata before mutation without discovery. The supplied instance keeps its external disposal owner; the container does not assume ownership.</remarks>
        public IServiceCollection AddToolProvider(ToolSourceId sourceId, IToolProvider provider)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNotEqual(provider.SourceId, sourceId, nameof(provider));
            return ToolServiceRegistration.RegisterProvider(services, sourceId, ServiceDescriptor.KeyedSingleton(sourceId, provider), false);
        }

        /// <summary>Explicitly replaces all provider registrations for one exact typed source key.</summary>
        /// <typeparam name="TProvider">The replacement singleton discovery provider with matching stable source identity.</typeparam>
        /// <param name="sourceId">The nondefault exact source key.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
        /// <exception cref="ArgumentException">Internal metadata cannot be inspected without activation.</exception>
        /// <remarks>Replacement never activates or disposes a provider. Other source keys, foreign-key registrations, old hosts, and retained bindings/captures remain unchanged. Missing sources are added.</remarks>
        public IServiceCollection ReplaceToolProvider<TProvider>(ToolSourceId sourceId) where TProvider : class, IToolProvider
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
            return ToolServiceRegistration.RegisterProvider(services, sourceId, ServiceDescriptor.KeyedSingleton<IToolProvider, TProvider>(sourceId), true);
        }

        /// <summary>Explicitly replaces one typed source with an existing borrowed provider instance.</summary>
        /// <param name="sourceId">The nondefault exact key matching the replacement provider's stable identity.</param>
        /// <param name="provider">The nonnull externally owned replacement provider.</param>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="provider"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="sourceId"/> is default.</exception>
        /// <exception cref="ArgumentException">Provider identity differs or registration metadata is malformed.</exception>
        /// <remarks>Validates before mutation, removes matching typed descriptors without activation or disposal, and preserves old hosts and captures. Both old and replacement supplied instances retain their external owners.</remarks>
        public IServiceCollection ReplaceToolProvider(ToolSourceId sourceId, IToolProvider provider)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentOutOfRangeException.ThrowIfEqual(sourceId, default);
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNotEqual(provider.SourceId, sourceId, nameof(provider));
            return ToolServiceRegistration.RegisterProvider(services, sourceId, ServiceDescriptor.KeyedSingleton(sourceId, provider), true);
        }

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

        /// <summary>Registers the replaceable internal coordinator chaining discovery, merge, and schema/capability preflight.</summary>
        /// <returns>The same collection for further composition.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Idempotent default registration composes <see cref="AddToolRegistrationCatalog"/>, <see cref="AddToolCatalogMerging"/>,
        /// and <see cref="AddToolSchemaEngine"/>, preserving host clock and logging choices. The default schema-preflight
        /// bounds match the built-in argument-validation defaults and are a reduced stand-in pending workstream 4's
        /// <c>ToolRuntimeOptions</c> (chunk C8). This coordinator is internal and not yet reachable through the public
        /// <c>IToolCatalog</c> surface (chunk C10b); it activates no service and discovers no source at registration time.
        /// </remarks>
        internal IServiceCollection AddToolCatalogCoordinator()
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddToolRegistrationCatalog();
            _ = services.AddToolCatalogMerging();
            _ = services.AddToolSchemaEngine();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<ToolCatalogVersion>>(
                static _ => new GuidIdentifierGenerator<ToolCatalogVersion>(static value => new ToolCatalogVersion(value.ToString())));
            services.TryAddSingleton(new ToolSchemaLimits(maximumUtf8Bytes: 262_144, maximumDepth: 64, maximumNodes: 10_000, maximumWork: 100_000));
            services.TryAddSingleton(static provider => new ToolCatalogCoordinator(
                provider.GetRequiredService<ToolCatalogDiscovery>(),
                provider.GetRequiredService<ToolCatalogMerger>(),
                provider.GetRequiredService<IToolSchemaEngine>(),
                provider.GetRequiredService<ToolSchemaLimits>(),
                provider.GetRequiredService<IIdentifierGenerator<ToolCatalogVersion>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<ToolCatalogCoordinator>>()));
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
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
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
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddToolPresentation();
            _ = services.AddToolResultProjectionPolicyCatalog();
            _ = services.AddToolSchemaEngine();
            var optionsBuilder = services.AddOptions<AgentToolsOptions>();
            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            _ = services.AddOptions<ToolRuntimeOptions>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IToolScheduler, BarrierSegmentToolScheduler>();

            services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
            services.TryAddSingleton<IToolResolver, ToolCallResolver>();
            services.TryAddSingleton<IToolArgumentValidator, ToolArgumentValidator>();
            services.TryAddSingleton<IToolResultNormalizer, ToolResultNormalizer>();
            services.TryAddSingleton<IToolResultProjector, ToolResultProjector>();
            services.TryAddSingleton(static provider =>
                provider.GetRequiredService<IOptions<AgentToolsOptions>>().Value.ArgumentValidationLimits);
            services.TryAddSingleton<IToolAuthorizer, AllowListToolAuthorizer>();
            services.TryAddSingleton<IToolCatalog>(static provider => new ToolCatalog(provider.GetServices<ITool>()));
            services.TryAddSingleton<ILegacyToolCallOrchestrator>(static provider => new DefaultToolInvoker(
                provider.GetRequiredService<IToolCatalog>(),
                provider.GetRequiredService<IToolAuthorizer>(),
                provider.GetRequiredService<ILogger<DefaultToolInvoker>>(),
                provider.GetRequiredService<IToolSchemaEngine>(),
                provider.GetRequiredService<IOptions<AgentToolsOptions>>().Value.ArgumentValidationLimits));
            services.TryAddSingleton<IToolExecutor>(static provider => new LegacyToolInvokerExecutor(
                provider.GetRequiredService<ILegacyToolCallOrchestrator>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<ILogger<LegacyToolInvokerExecutor>>()));
            services.TryAddSingleton<IToolRunCatalogCaptureFactory, LegacyToolRunCatalogCaptureFactory>();

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
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddTool<TTool>()
            where TTool : class, ITool
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddSingleton<ITool, TTool>();
            return services;
        }
    }

}
