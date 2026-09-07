// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>Dependency-injection registration for keyed output-processing profiles.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the built-in output profile under the explicit default processor key.</summary>
        /// <param name="configure">Optional configuration captured only by the first registration of the default key.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured numeric limit is outside its documented range.</exception>
        /// <remarks>
        /// The default schema engine, options snapshot, and definition registry are singletons; the processor is scoped.
        /// Repeated registration is idempotent and retains the first captured configuration.
        /// </remarks>
        public IServiceCollection AddAgentOutput(Action<AgentOutputOptions>? configure = null) =>
            services.AddAgentOutput(AgentOutputDefaults.ProcessorKey, configure);

        /// <summary>Registers one keyed built-in output-processing profile.</summary>
        /// <param name="processorKey">The stable processor-profile key.</param>
        /// <param name="configure">Optional configuration captured only by the first registration of this key.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="processorKey"/> is uninitialized.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A configured numeric limit is outside its documented range.</exception>
        /// <remarks>Repeated registration of the same key is idempotent; its first captured configuration wins.</remarks>
        public IServiceCollection AddAgentOutput(
            ComponentKey<IOutputProcessor> processorKey,
            Action<AgentOutputOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(processorKey.Value, nameof(processorKey));
            if (services.Any(descriptor => descriptor.ServiceType == typeof(OutputProfileRegistration)
                && descriptor.ImplementationInstance is OutputProfileRegistration marker
                && marker.ProcessorKey.Equals(processorKey)))
            {
                return services;
            }

            var options = new AgentOutputOptions();
            configure?.Invoke(options);
            var snapshot = OutputRegistration.CreateSnapshot(options);
            var serviceKey = processorKey.Value;

            _ = services.AddAgentKitObservability();
            _ = services.AddSingleton(new OutputProfileRegistration(processorKey));
            services.TryAddKeyedSingleton<IOutputSchemaEngine, StructuralOutputSchemaEngine>(serviceKey);
            services.TryAddKeyedSingleton(serviceKey, snapshot);
            services.TryAddKeyedSingleton<IOutputDefinitionResolver>(serviceKey, (provider, _) =>
                new InMemoryOutputDefinitionRegistry(
                    provider.GetKeyedServices<OutputDefinition>(serviceKey),
                    provider.GetRequiredKeyedService<IOutputSchemaEngine>(serviceKey),
                    provider.GetRequiredKeyedService<AgentOutputOptionsSnapshot>(serviceKey),
                    provider.GetService<ILogger<InMemoryOutputDefinitionRegistry>>()));
            services.TryAddKeyedScoped<IOutputProcessor>(serviceKey, (provider, _) =>
                new DefaultOutputProcessor(
                    provider.GetKeyedServices<IOutputValidator>(serviceKey),
                    provider.GetRequiredKeyedService<IOutputSchemaEngine>(serviceKey),
                    provider.GetRequiredKeyedService<AgentOutputOptionsSnapshot>(serviceKey),
                    provider.GetService<ILogger<DefaultOutputProcessor>>()));

            if (processorKey.Equals(AgentOutputDefaults.ProcessorKey))
            {
                services.TryAddSingleton(provider =>
                    provider.GetRequiredKeyedService<IOutputSchemaEngine>(AgentOutputDefaults.ProcessorKey.Value));
                services.TryAddSingleton(provider =>
                    provider.GetRequiredKeyedService<IOutputDefinitionResolver>(AgentOutputDefaults.ProcessorKey.Value));
                services.TryAddScoped(provider =>
                    provider.GetRequiredKeyedService<IOutputProcessor>(AgentOutputDefaults.ProcessorKey.Value));
            }

            return services;
        }

        /// <summary>Additively registers a definition for the explicit default output profile.</summary>
        /// <param name="definition">The immutable definition to register.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
        /// <remarks>Definitions are additive singleton registrations under the explicit default profile.</remarks>
        public IServiceCollection AddOutputDefinition(OutputDefinition definition) =>
            services.AddOutputDefinition(AgentOutputDefaults.ProcessorKey, definition);

        /// <summary>Additively registers a definition for one processor profile.</summary>
        /// <param name="processorKey">The profile that owns the definition.</param>
        /// <param name="definition">The immutable definition to register.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="definition"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="processorKey"/> is uninitialized.</exception>
        /// <remarks>Definitions are additive singleton registrations; duplicate definition identities are rejected when the keyed registry is resolved.</remarks>
        public IServiceCollection AddOutputDefinition(
            ComponentKey<IOutputProcessor> processorKey,
            OutputDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(processorKey.Value, nameof(processorKey));
            ArgumentNullException.ThrowIfNull(definition);
            return services.AddKeyedSingleton(processorKey.Value, definition);
        }

        /// <summary>Replaces the processor under the explicit default output profile.</summary>
        /// <typeparam name="TProcessor">The scoped processor implementation.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <remarks>Every existing default-key processor descriptor is removed before the scoped replacement is added.</remarks>
        public IServiceCollection ReplaceOutputProcessor<TProcessor>()
            where TProcessor : class, IOutputProcessor =>
            services.ReplaceOutputProcessor<TProcessor>(AgentOutputDefaults.ProcessorKey);

        /// <summary>Replaces the scoped processor for one output profile without changing its schema engine.</summary>
        /// <typeparam name="TProcessor">The scoped processor implementation.</typeparam>
        /// <param name="processorKey">The profile whose processor is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="processorKey"/> is uninitialized.</exception>
        /// <remarks>Every processor descriptor at the exact key is removed before the scoped replacement is added.</remarks>
        public IServiceCollection ReplaceOutputProcessor<TProcessor>(ComponentKey<IOutputProcessor> processorKey)
            where TProcessor : class, IOutputProcessor
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(processorKey.Value, nameof(processorKey));
            OutputRegistration.RemoveKeyed<IOutputProcessor>(services, processorKey.Value);
            _ = services.AddKeyedScoped<IOutputProcessor, TProcessor>(processorKey.Value);
            return services;
        }

        /// <summary>Replaces the schema engine under the explicit default output profile.</summary>
        /// <typeparam name="TEngine">The stateless schema-engine implementation.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <remarks>The replacement is a singleton and does not replace the processor or waive definition preflight.</remarks>
        public IServiceCollection ReplaceOutputSchemaEngine<TEngine>()
            where TEngine : class, IOutputSchemaEngine =>
            services.ReplaceOutputSchemaEngine<TEngine>(AgentOutputDefaults.ProcessorKey);

        /// <summary>Replaces the singleton schema engine for one profile without changing its processor.</summary>
        /// <typeparam name="TEngine">The stateless schema-engine implementation.</typeparam>
        /// <param name="processorKey">The profile whose schema engine is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="processorKey"/> is uninitialized.</exception>
        /// <remarks>Every schema-engine descriptor at the exact key is removed before the singleton replacement is added.</remarks>
        public IServiceCollection ReplaceOutputSchemaEngine<TEngine>(ComponentKey<IOutputProcessor> processorKey)
            where TEngine : class, IOutputSchemaEngine
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(processorKey.Value, nameof(processorKey));
            OutputRegistration.RemoveKeyed<IOutputSchemaEngine>(services, processorKey.Value);
            _ = services.AddKeyedSingleton<IOutputSchemaEngine, TEngine>(processorKey.Value);
            return services;
        }

        /// <summary>Additively registers a stateless validator under the explicit default output profile.</summary>
        /// <typeparam name="TValidator">The validator implementation.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <remarks>Validators are additive singleton registrations and must be stateless and thread-safe.</remarks>
        public IServiceCollection AddOutputValidator<TValidator>()
            where TValidator : class, IOutputValidator =>
            services.AddOutputValidator<TValidator>(AgentOutputDefaults.ProcessorKey);

        /// <summary>Additively registers a stateless singleton validator for one output profile.</summary>
        /// <typeparam name="TValidator">The validator implementation.</typeparam>
        /// <param name="processorKey">The profile that owns the validator.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="processorKey"/> is uninitialized.</exception>
        /// <remarks>Validators are additive singleton registrations isolated to the exact profile key and must be stateless and thread-safe.</remarks>
        public IServiceCollection AddOutputValidator<TValidator>(ComponentKey<IOutputProcessor> processorKey)
            where TValidator : class, IOutputValidator
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(processorKey.Value, nameof(processorKey));
            return services.AddKeyedSingleton<IOutputValidator, TValidator>(processorKey.Value);
        }
    }
}
