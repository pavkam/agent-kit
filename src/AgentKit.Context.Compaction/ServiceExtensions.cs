// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Dependency-injection registration for the built-in compaction pipeline,
/// with either the deterministic extractive strategy or the model-backed
/// summary strategy.
/// </summary>
/// <remarks>
/// Both entry points register exactly one <see cref="ICompactor"/> composed
/// from one <see cref="ICompactionCutSelector"/>, one
/// <see cref="ICompactionStrategy"/>, and one <see cref="ICompactionValidator"/>
/// — <see cref="StructuralCompactionCutSelector"/>, the selected strategy, and
/// <see cref="DefaultCompactionValidator"/> respectively. Composition requires
/// an <see cref="ISessionCoordinator"/> to already be registered (for example
/// through <c>AddAgentSession</c>); neither method registers one itself. The
/// model-backed variant additionally requires the engine's
/// <see cref="IModelCatalog"/>, <see cref="IModelSelector"/>, and
/// <see cref="ILlmModelResolver"/> (for example through the
/// <c>AgentKit.Providers</c> registration) plus at least one branded provider
/// package supplying the selected model.
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
        /// so calling this more than once keeps the first registration, and
        /// calling it after <see cref="AddModelBackedContextCompaction"/>
        /// keeps the model-backed strategy. Options are validated on first
        /// access: every numeric bound must be positive;
        /// <see cref="CompactionOptions.MaximumCheckpointCharacters"/> and
        /// <see cref="CompactionOptions.MaximumSummaryInputCharacters"/> must
        /// exceed the length of <see cref="ExtractiveCompactionStrategy.TruncationMarker"/>,
        /// because a smaller ceiling leaves no room for retained text and would
        /// fail every attempt deterministically; and
        /// <see cref="CompactionOptions.SummaryPrompt"/> must not be null,
        /// empty, or whitespace so a model-backed strategy registered against
        /// the same options never sends an empty instruction.
        /// </remarks>
        public IServiceCollection AddContextCompaction(Action<CompactionOptions>? configure = null)
        {
            _ = services.AddAgentKitObservability();
            var optionsBuilder = services.AddOptions<CompactionOptions>()
                .Validate(o => o.CharactersPerToken > 0, "CharactersPerToken must be positive.")
                .Validate(o => o.MaximumSourceEntries > 0, "MaximumSourceEntries must be positive.")
                .Validate(
                    static o => o.MaximumCheckpointCharacters > ExtractiveCompactionStrategy.TruncationMarker.Length,
                    $"MaximumCheckpointCharacters must exceed the {ExtractiveCompactionStrategy.TruncationMarker.Length}-character truncation marker.")
                .Validate(
                    static o => o.MaximumSummaryInputCharacters > ModelCompactionStrategy.TruncationMarker.Length,
                    $"MaximumSummaryInputCharacters must exceed the {ModelCompactionStrategy.TruncationMarker.Length}-character truncation marker.")
                .Validate(o => o.SourceReadPageSize > 0, "SourceReadPageSize must be positive.")
                .Validate(static o => !string.IsNullOrWhiteSpace(o.SummaryPrompt), "SummaryPrompt must not be null, empty, or whitespace.");

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

        /// <summary>
        /// Registers the built-in compaction pipeline with <see cref="ModelCompactionStrategy"/> as its
        /// <see cref="ICompactionStrategy"/>, so checkpoints are model-generated summaries produced under
        /// <see cref="CompactionOptions.SummaryPrompt"/>.
        /// </summary>
        /// <param name="configure">
        /// Optional configuration for <see cref="CompactionOptions"/>. At minimum
        /// <see cref="CompactionOptions.SummaryModelPolicy"/> must be set, because the summary model is an external
        /// fact the application names; <see cref="CompactionOptions.SummaryPrompt"/> may be overridden here.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <remarks>
        /// <para>
        /// This method first applies <see cref="AddContextCompaction"/> with the same <paramref name="configure"/>,
        /// then replaces the single <see cref="ICompactionStrategy"/> registration with
        /// <see cref="ModelCompactionStrategy"/> and adds, with <c>TryAdd</c> semantics, the
        /// <see cref="IIdentifierGenerator{TIdentifier}"/> registrations for <see cref="ModelRequestId"/> and
        /// <see cref="MessageId"/> that the strategy needs. Calling it more than once is idempotent; calling it after
        /// <see cref="AddContextCompaction"/> switches the already registered pipeline to the model-backed strategy
        /// without duplicating any other collaborator.
        /// </para>
        /// <para>
        /// In addition to the checks documented on <see cref="AddContextCompaction"/>, options validation requires
        /// <see cref="CompactionOptions.SummaryModelPolicy"/> to be non-null. It does not register
        /// <see cref="IModelCatalog"/>, <see cref="IModelSelector"/>, or <see cref="ILlmModelResolver"/>; the
        /// application selects those through its provider registrations, and a missing registration fails when the
        /// strategy is first resolved.
        /// </para>
        /// </remarks>
        public IServiceCollection AddModelBackedContextCompaction(Action<CompactionOptions>? configure = null)
        {
            _ = services.AddContextCompaction(configure);
            _ = services.AddOptions<CompactionOptions>()
                .Validate(
                    static o => o.SummaryModelPolicy is not null,
                    "SummaryModelPolicy must name at least one candidate alias for model-backed compaction.");

            services.TryAddSingleton<IIdentifierGenerator<ModelRequestId>>(
                _ => new GuidIdentifierGenerator<ModelRequestId>(static value => new ModelRequestId(value)));
            services.TryAddSingleton<IIdentifierGenerator<MessageId>>(
                _ => new GuidIdentifierGenerator<MessageId>(static value => new MessageId(value)));
            _ = services.Replace(ServiceDescriptor.Singleton<ICompactionStrategy, ModelCompactionStrategy>());

            return services;
        }
    }
}
