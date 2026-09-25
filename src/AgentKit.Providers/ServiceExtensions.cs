// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using Microsoft.Extensions.Options;

/// <summary>
/// Registers the provider-neutral model catalog, selector, and capability
/// validator.
/// </summary>
/// <remarks>
/// This package contains no vendor protocol. Concrete provider packages
/// contribute descriptors and LLM model implementations; nothing here
/// resolves credentials, chooses an endpoint, or performs network access.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the first-party model catalog, selector, capability
        /// validator, LLM model resolver, and embedding model resolver.
        /// </summary>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> so registrations can be
        /// chained.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Every registration uses <c>TryAdd</c> semantics, so calling this
        /// method more than once is idempotent and an application that
        /// registered its own catalog, selector, or validator first keeps it.
        /// </para>
        /// <para>
        /// This method registers no descriptor source. A composition with no
        /// source produces an empty catalog, and selection then reports
        /// <see cref="NoCompatibleModel"/> rather than silently inventing a
        /// default model.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The service collection is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddAgentProviders(Action<AgentProviderRuntimeOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddAgentKitObservability();
            _ = services.AddOptions<AgentProviderRuntimeOptions>();
            if (configure is not null)
            {
                _ = services.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IModelCapabilityValidator, DefaultModelCapabilityValidator>();
            services.TryAddSingleton<IModelCatalog, DefaultModelCatalog>();
            services.TryAddSingleton<IModelSelector, DefaultModelSelector>();
            services.TryAddSingleton<ILlmModelResolver, DefaultLlmModelResolver>();
            services.TryAddSingleton<IEmbeddingModelResolver, DefaultEmbeddingModelResolver>();
            services.TryAddSingleton<IModelRequestExecutor, DefaultModelRequestExecutor>();
            services.TryAddSingleton<IEmbeddingModelSelector, DefaultEmbeddingModelSelector>();
            services.TryAddSingleton<IEmbeddingRequestExecutor, DefaultEmbeddingRequestExecutor>();
            services.TryAddSingleton<IRerankerSelector, DefaultRerankerSelector>();
            services.TryAddSingleton<IRerankerResolver, DefaultRerankerResolver>();
            services.TryAddSingleton<IRerankRequestExecutor, DefaultRerankRequestExecutor>();
            services.TryAddSingleton(CreateProfileRegistry);
            services.TryAddSingleton<IProviderProfileRuntimeSelector, DefaultProviderProfileRuntimeSelector>();

            return services;
        }

        /// <summary>
        /// Replaces the registered <see cref="IModelRequestExecutor"/> with another implementation.
        /// </summary>
        /// <typeparam name="TExecutor">The replacement executor type.</typeparam>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        /// <exception cref="ArgumentNullException">The service collection is <see langword="null"/>.</exception>
        public IServiceCollection ReplaceModelRequestExecutor<TExecutor>()
            where TExecutor : class, IModelRequestExecutor
        {
            ArgumentNullException.ThrowIfNull(services);
            return services.Replace(ServiceDescriptor.Singleton<IModelRequestExecutor, TExecutor>());
        }

        /// <summary>
        /// Adds a fixed set of conversational model descriptors to the
        /// engine-wide catalog.
        /// </summary>
        /// <param name="sourceId">
        /// The stable identity of this contribution, used to attribute
        /// duplicate-alias errors.
        /// </param>
        /// <param name="conversationModels">
        /// The descriptors to publish. An empty set is valid.
        /// </param>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> so registrations can be
        /// chained.
        /// </returns>
        /// <remarks>
        /// Descriptor sources are additive and composed in registration
        /// order. Registering two sources that publish the same alias is a
        /// composition error surfaced when the catalog is first read, not a
        /// last-one-wins merge.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The service collection is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="conversationModels"/> is uninitialized or contains
        /// <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddModelDescriptors(
            ModelDescriptorSourceId sourceId,
            ImmutableArray<ModelDescriptor> conversationModels)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfContainsNull(conversationModels);

            _ = services.AddSingleton<IModelDescriptorSource>(
                new StaticModelDescriptorSource(sourceId, conversationModels));

            return services;
        }

        /// <summary>
        /// Adds a custom descriptor source, such as live provider discovery.
        /// </summary>
        /// <typeparam name="TSource">The source implementation type.</typeparam>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> so registrations can be
        /// chained.
        /// </returns>
        /// <remarks>
        /// Sources are additive; this method never replaces an existing
        /// registration. The source is registered as a singleton because the
        /// catalog reads it concurrently.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// The service collection is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddModelDescriptorSource<TSource>()
            where TSource : class, IModelDescriptorSource
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddSingleton<IModelDescriptorSource, TSource>();
            return services;
        }

        /// <summary>Registers one endpoint profile snapshot.</summary>
        /// <param name="key">The stable profile key.</param>
        /// <param name="configure">Configures the profile options.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection AddProviderEndpointProfile(
            ProviderEndpointProfileKey key,
            Action<ProviderEndpointProfileOptions> configure) =>
            RegisterEndpointProfile(services, key, configure, replace: false);

        /// <summary>Registers one endpoint profile snapshot resolved from the built service provider.</summary>
        /// <param name="key">The stable profile key.</param>
        /// <param name="configure">Configures the profile using live services.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection AddProviderEndpointProfileFromServices(
            ProviderEndpointProfileKey key,
            Action<ProviderEndpointProfileOptions, IServiceProvider> configure) =>
            RegisterEndpointProfileFromServices(services, key, configure, replace: false);

        /// <summary>Replaces one endpoint profile snapshot.</summary>
        /// <param name="key">The stable profile key.</param>
        /// <param name="configure">Configures the profile options.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection ReplaceProviderEndpointProfile(
            ProviderEndpointProfileKey key,
            Action<ProviderEndpointProfileOptions> configure) =>
            RegisterEndpointProfile(services, key, configure, replace: true);

        /// <summary>Registers one credential profile snapshot.</summary>
        /// <param name="key">The stable profile key.</param>
        /// <param name="configure">Configures the profile options.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection AddProviderCredentialProfile(
            ProviderCredentialProfileKey key,
            Action<ProviderCredentialProfileOptions> configure) =>
            RegisterCredentialProfile(services, key, configure, replace: false);

        /// <summary>Replaces one credential profile snapshot.</summary>
        /// <param name="key">The stable profile key.</param>
        /// <param name="configure">Configures the profile options.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection ReplaceProviderCredentialProfile(
            ProviderCredentialProfileKey key,
            Action<ProviderCredentialProfileOptions> configure) =>
            RegisterCredentialProfile(services, key, configure, replace: true);

        /// <summary>Registers a keyed credential source.</summary>
        /// <typeparam name="TSource">The credential source implementation.</typeparam>
        /// <param name="key">The source key referenced by credential profiles.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection AddProviderCredentialSource<TSource>(ProviderCredentialSourceKey key)
            where TSource : class, IProviderCredentialSource
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IProviderProfileRuntimeSelector, DefaultProviderProfileRuntimeSelector>();
            _ = services.AddKeyedSingleton<IProviderCredentialSource, TSource>(key);
            return services;
        }

        /// <summary>Replaces a keyed credential source registration.</summary>
        /// <typeparam name="TSource">The replacement credential source implementation.</typeparam>
        /// <param name="key">The source key referenced by credential profiles.</param>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection ReplaceProviderCredentialSource<TSource>(ProviderCredentialSourceKey key)
            where TSource : class, IProviderCredentialSource
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.RemoveAllKeyed<IProviderCredentialSource>(key);
            _ = services.AddKeyedSingleton<IProviderCredentialSource, TSource>(key);
            return services;
        }

        /// <summary>Replaces the profile runtime selector.</summary>
        /// <typeparam name="TSelector">The replacement selector type.</typeparam>
        /// <returns>The same <see cref="IServiceCollection"/> so registrations can be chained.</returns>
        public IServiceCollection ReplaceProviderProfileRuntimeSelector<TSelector>()
            where TSelector : class, IProviderProfileRuntimeSelector
        {
            ArgumentNullException.ThrowIfNull(services);
            return services.Replace(ServiceDescriptor.Singleton<IProviderProfileRuntimeSelector, TSelector>());
        }
    }

    private static ProviderProfileRegistry CreateProfileRegistry(IServiceProvider serviceProvider)
    {
        var registry = new ProviderProfileRegistry();
        foreach (var configure in serviceProvider.GetServices<IConfigureOptions<ProviderProfileRegistry>>())
        {
            configure.Configure(registry);
        }

        return registry;
    }

    private static IServiceCollection RegisterEndpointProfile(
        IServiceCollection services,
        ProviderEndpointProfileKey key,
        Action<ProviderEndpointProfileOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.TryAddSingleton(CreateProfileRegistry);
        services.TryAddSingleton<IProviderProfileRuntimeSelector, DefaultProviderProfileRuntimeSelector>();
        _ = services.AddSingleton<IConfigureOptions<ProviderProfileRegistry>>(new ConfigureEndpointProfile(key, configure, replace));
        return services;
    }

    private static IServiceCollection RegisterEndpointProfileFromServices(
        IServiceCollection services,
        ProviderEndpointProfileKey key,
        Action<ProviderEndpointProfileOptions, IServiceProvider> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.TryAddSingleton(CreateProfileRegistry);
        services.TryAddSingleton<IProviderProfileRuntimeSelector, DefaultProviderProfileRuntimeSelector>();
        _ = services.AddSingleton<IConfigureOptions<ProviderProfileRegistry>, ConfigureEndpointProfileFromServices>(
            sp => new ConfigureEndpointProfileFromServices(sp, key, configure, replace));
        return services;
    }

    private static IServiceCollection RegisterCredentialProfile(
        IServiceCollection services,
        ProviderCredentialProfileKey key,
        Action<ProviderCredentialProfileOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.TryAddSingleton(CreateProfileRegistry);
        services.TryAddSingleton<IProviderProfileRuntimeSelector, DefaultProviderProfileRuntimeSelector>();
        _ = services.AddSingleton<IConfigureOptions<ProviderProfileRegistry>>(new ConfigureCredentialProfile(key, configure, replace));
        return services;
    }

    private sealed class ConfigureEndpointProfile(
        ProviderEndpointProfileKey key,
        Action<ProviderEndpointProfileOptions> configure,
        bool replace): IConfigureOptions<ProviderProfileRegistry>
    {
        public void Configure(ProviderProfileRegistry registry)
        {
            var options = new ProviderEndpointProfileOptions();
            configure(options);
            registry.RegisterEndpoint(ProviderProfileSnapshots.CreateEndpoint(key, options), replace);
        }
    }

    private sealed class ConfigureEndpointProfileFromServices(
        IServiceProvider serviceProvider,
        ProviderEndpointProfileKey key,
        Action<ProviderEndpointProfileOptions, IServiceProvider> configure,
        bool replace): IConfigureOptions<ProviderProfileRegistry>
    {
        public void Configure(ProviderProfileRegistry registry)
        {
            var options = new ProviderEndpointProfileOptions();
            configure(options, serviceProvider);
            registry.RegisterEndpoint(ProviderProfileSnapshots.CreateEndpoint(key, options), replace);
        }
    }

    private sealed class ConfigureCredentialProfile(
        ProviderCredentialProfileKey key,
        Action<ProviderCredentialProfileOptions> configure,
        bool replace): IConfigureOptions<ProviderProfileRegistry>
    {
        public void Configure(ProviderProfileRegistry registry)
        {
            var options = new ProviderCredentialProfileOptions();
            configure(options);
            registry.RegisterCredential(ProviderProfileSnapshots.CreateCredential(key, options), replace);
        }
    }
}
