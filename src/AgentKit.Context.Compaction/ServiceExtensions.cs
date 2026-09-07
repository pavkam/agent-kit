// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the built-in, non-model-backed
/// compaction pipeline.
/// </summary>
/// <remarks>
/// This registers exactly one <see cref="ICompactor"/> composed from one
/// <see cref="ICompactionCutSelector"/>, one <see cref="ICompactionStrategy"/>,
/// and one <see cref="ICompactionValidator"/> — <see cref="StructuralCompactionCutSelector"/>,
/// <see cref="ExtractiveCompactionStrategy"/>, and
/// <see cref="DefaultCompactionValidator"/> respectively. Composition
/// requires an <see cref="ISessionCoordinator"/> to already be registered
/// (for example through <c>AddAgentSession</c>); this method does not
/// register one itself.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the built-in extractive compaction pipeline.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="CompactionOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once keeps the first registration.
        /// </remarks>
        public IServiceCollection AddContextCompaction(Action<CompactionOptions>? configure = null)
        {
            _ = services.AddAgentKitObservability();
            var optionsBuilder = services.AddOptions<CompactionOptions>()
                .Validate(o => o.CharactersPerToken > 0, "CharactersPerToken must be positive.")
                .Validate(o => o.MaximumSourceEntries > 0, "MaximumSourceEntries must be positive.")
                .Validate(o => o.MaximumCheckpointCharacters > 0, "MaximumCheckpointCharacters must be positive.")
                .Validate(o => o.SourceReadPageSize > 0, "SourceReadPageSize must be positive.");

            if (configure is not null)
            {
                _ = optionsBuilder.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<CompactionManifestId>>(
                _ => new GuidIdentifierGenerator<CompactionManifestId>(static value => new CompactionManifestId(value)));
            services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>>(
                _ => new GuidIdentifierGenerator<SessionEntryId>(static value => new SessionEntryId(value)));
            services.TryAddSingleton<ICompactionSizeEstimator, CharacterCompactionSizeEstimator>();
            services.TryAddSingleton<ICompactionCutSelector, StructuralCompactionCutSelector>();
            services.TryAddSingleton<ICompactionStrategy, ExtractiveCompactionStrategy>();
            services.TryAddSingleton<ICompactionValidator, DefaultCompactionValidator>();
            services.TryAddSingleton<ICompactor, DefaultCompactor>();

            return services;
        }
    }
}
