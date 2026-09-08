// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Provides dependency-injection registration for the AgentKit facade, its
/// agent-definition catalog, and its replaceable foundation defaults.
/// </summary>
/// <remarks>
/// These methods only modify the supplied service collection. They never build
/// a provider. Feature packages register loops, providers, sessions, tools, and
/// other runtime components independently against the same collection.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the host-managed AgentKit facade, its foundation clock,
        /// its run-identity generator, and the first-party agent-definition
        /// catalog.
        /// </summary>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Registration is idempotent and uses <c>TryAdd</c> semantics. The
        /// engine, clock, and catalog are singular, thread-safe singletons. A
        /// resolved engine does not own the external host's provider; the host
        /// remains responsible for scopes, shutdown, and disposal.
        /// </para>
        /// <para>
        /// This registers no loop, provider, store, tool, or security
        /// authority. It also registers no agent definition: a composition
        /// with none fails validation rather than inventing a default agent.
        /// </para>
        /// </remarks>
        public IServiceCollection AddAgentKit()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<RunId>>(
                new DelegateIdentifierGenerator<RunId>(static () => new RunId(Guid.NewGuid())));
            services.TryAddSingleton<IIdentifierGenerator<OperationId>>(
                new DelegateIdentifierGenerator<OperationId>(static () => new OperationId(Guid.NewGuid())));
            services.TryAddSingleton<IAgentDefinitionCatalog, DefaultAgentDefinitionCatalog>();
            services.TryAddSingleton<IAgentRunProfilePublicationReader, DefaultAgentRunProfilePublicationReader>();
            services.TryAddSingleton(
                static provider =>
                {
                    var runProfiles = AgentCompositionValidator.Validate(provider);
                    return new AgentEngine(provider, ownedProvider: null, runProfiles);
                });

            return services;
        }

        /// <summary>
        /// Publishes one agent definition to the engine-wide catalog.
        /// </summary>
        /// <param name="definition">The immutable definition to publish.</param>
        /// <param name="precedence">
        /// The precedence of this registration when another source publishes
        /// the same <see cref="AgentId"/>. Higher wins; equal precedence is a
        /// composition error.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Each call registers its own additive definition source, keyed by
        /// the agent's identity, so publishing several agents is a sequence of
        /// independent registrations rather than one mutable list.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> or <paramref name="definition"/> is
        /// <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddAgent(AgentDefinition definition, int precedence = 0)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(definition);

            var snapshot = new AgentDefinitionSourceSnapshot(
                new AgentDefinitionSourceId($"agent:{definition.Id}"),
                new AgentDefinitionSourceVersion(0),
                precedence,
                [definition]);
            var hasSnapshot = services.Any(descriptor =>
                descriptor.ServiceType == typeof(AgentDefinitionSourceSnapshot)
                && descriptor.ImplementationInstance is AgentDefinitionSourceSnapshot existing
                && existing.SourceId == snapshot.SourceId
                && existing.Equals(snapshot));
            var hasSource = services.Any(descriptor =>
                descriptor.ServiceType == typeof(IAgentDefinitionSource)
                && descriptor.ImplementationInstance is IAgentDefinitionSource existing
                && existing.SourceId == snapshot.SourceId);
            if (hasSnapshot && hasSource)
            {
                return services;
            }

            if (!hasSnapshot)
            {
                _ = services.AddAgentDefinitionSnapshot(snapshot);
            }

            if (!hasSource)
            {
                _ = services.AddSingleton<IAgentDefinitionSource>(
                    new StaticAgentDefinitionSource(
                        snapshot.SourceId,
                        [definition],
                        precedence));
            }

            return services;
        }

        /// <summary>Publishes one immutable exact run-profile binding to the built composition.</summary>
        /// <param name="publication">The non-null host-owned immutable security and session profile binding.</param>
        /// <returns>The same service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="publication"/> is null.</exception>
        /// <remarks>Registrations are additive. The default reader rejects duplicate agent and definition-revision coordinates during activation. The contained security publication is registered from the same instance for the exact security-profile reader.</remarks>
        public IServiceCollection AddAgentRunProfilePublication(AgentRunProfilePublication publication)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(publication);
            _ = services.AddSingleton(publication);
            _ = services.AddSingleton(publication.SecurityProfile);
            return services;
        }

        /// <summary>Registers one trusted, already materialized source contribution for startup composition.</summary>
        /// <param name="snapshot">The immutable contribution captured by the host before building the engine.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="snapshot"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">A different bootstrap contribution already uses the same source identity.</exception>
        /// <remarks>No provider is built and no source is read. Repeating the exact contribution is idempotent.</remarks>
        public IServiceCollection AddAgentDefinitionSnapshot(AgentDefinitionSourceSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(snapshot);

            foreach (var descriptor in services.Where(static descriptor => descriptor.ServiceType == typeof(AgentDefinitionSourceSnapshot)))
            {
                if (descriptor.ImplementationInstance is not AgentDefinitionSourceSnapshot existing || existing.SourceId != snapshot.SourceId)
                {
                    continue;
                }

                return existing.Equals(snapshot)
                    ? services
                    : throw new InvalidOperationException(
                        $"Bootstrap source '{snapshot.SourceId}' is already registered with different content.");
            }

            _ = services.AddSingleton(snapshot);
            return services;
        }

        /// <summary>
        /// Adds a custom agent-definition source, such as one backed by a
        /// database or remote control plane.
        /// </summary>
        /// <typeparam name="TSource">The source implementation type.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Sources are additive; this never replaces an existing registration.
        /// The source is registered as a singleton because the catalog reads
        /// it concurrently.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> is <see langword="null"/>.
        /// </exception>
        public IServiceCollection AddAgentDefinitionSource<TSource>()
            where TSource : class, IAgentDefinitionSource
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddSingleton<IAgentDefinitionSource, TSource>();
            return services;
        }

        /// <summary>
        /// Replaces the engine-wide clock with a caller-supplied instance.
        /// </summary>
        /// <param name="timeProvider">
        /// The non-null clock to capture in engines built or resolved after the
        /// replacement.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="services"/> or <paramref name="timeProvider"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// This is an explicit singular replacement: all existing
        /// <see cref="TimeProvider"/> descriptors are removed before the
        /// supplied instance is registered. As with Microsoft DI instance
        /// registrations, the caller retains disposal ownership of the
        /// instance. Existing engines retain their previously captured clock.
        /// </remarks>
        public IServiceCollection ReplaceTimeProvider(TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(timeProvider);

            _ = services.RemoveAll<TimeProvider>();
            _ = services.AddSingleton(timeProvider);
            return services;
        }
    }
}
