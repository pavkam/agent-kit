// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

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
        public IServiceCollection AddAgentProviders()
        {
            ArgumentNullException.ThrowIfNull(services);

            _ = services.AddAgentKitObservability();

            services.TryAddSingleton<IModelCapabilityValidator, DefaultModelCapabilityValidator>();
            services.TryAddSingleton<IModelCatalog, DefaultModelCatalog>();
            services.TryAddSingleton<IModelSelector, DefaultModelSelector>();
            services.TryAddSingleton<ILlmModelResolver, DefaultLlmModelResolver>();
            services.TryAddSingleton<IEmbeddingModelResolver, DefaultEmbeddingModelResolver>();

            return services;
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
    }
}
