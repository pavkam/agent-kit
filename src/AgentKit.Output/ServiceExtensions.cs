// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency-injection registration for the built-in, reduced-scope output
/// processor.
/// </summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the built-in <see cref="DefaultOutputProcessor"/> and definition resolver.</summary>
        /// <param name="configure">Optional configuration for <see cref="AgentOutputOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once keeps the first registration.
        /// </remarks>
        public IServiceCollection AddAgentOutput(Action<AgentOutputOptions>? configure = null)
        {
            var optionsBuilder = services.AddOptions<AgentOutputOptions>()
                .Validate(static o => o.MaximumCandidateBytes > 0, "MaximumCandidateBytes must be positive.")
                .Validate(static o => o.MaximumValidationIssues > 0, "MaximumValidationIssues must be positive.")
                .Validate(static o => o.MaximumRepairAttempts >= 0, "MaximumRepairAttempts must not be negative.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(static provider =>
            {
                var options = provider.GetRequiredService<IOptions<AgentOutputOptions>>().Value;
                return new AgentOutputOptionsSnapshot(
                    options.MaximumCandidateBytes,
                    options.MaximumValidationIssues,
                    options.MaximumRepairAttempts,
                    options.RequireSchemaForStructuredModes,
                    options.AllowProviderModeDowngrade);
            });

            services.TryAddSingleton<IOutputDefinitionResolver, InMemoryOutputDefinitionRegistry>();
            services.TryAddSingleton<IOutputProcessor, DefaultOutputProcessor>();

            return services;
        }

        /// <summary>Additively registers an immutable <see cref="OutputDefinition"/>.</summary>
        /// <param name="definition">The definition to register.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        /// <remarks>
        /// Registering more than one definition with the same identity and
        /// version fails when <see cref="IOutputDefinitionResolver"/> is
        /// built.
        /// </remarks>
        public IServiceCollection AddOutputDefinition(OutputDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            return services.AddSingleton(definition);
        }

        /// <summary>Replaces the singular <see cref="IOutputProcessor"/> with <typeparamref name="TProcessor"/>.</summary>
        /// <typeparam name="TProcessor">The processor implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        public IServiceCollection ReplaceOutputProcessor<TProcessor>()
            where TProcessor : class, IOutputProcessor
        {
            _ = services.RemoveAll<IOutputProcessor>();
            _ = services.AddSingleton<IOutputProcessor, TProcessor>();
            return services;
        }

        /// <summary>Additively registers an <see cref="IOutputValidator"/>.</summary>
        /// <typeparam name="TValidator">The validator implementation to register.</typeparam>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Registering more than one validator with the same
        /// <see cref="IOutputValidator.Name"/> fails when
        /// <see cref="IOutputProcessor"/> is built.
        /// </remarks>
        public IServiceCollection AddOutputValidator<TValidator>()
            where TValidator : class, IOutputValidator =>
            services.AddSingleton<IOutputValidator, TValidator>();
    }
}
