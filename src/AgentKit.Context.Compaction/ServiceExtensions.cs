// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// Dependency-injection registration for the built-in compaction pipeline: keyed compactors, profiles, strategies,
/// summary generators, event sinks, and the per-collaborator replacements.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AddAgentContextCompaction"/> registers one <see cref="ICompactor"/> under an explicit key, composed from a
/// cut selector, a strategy resolver, a validator, an activation coordinator, and an event dispatcher that are each
/// keyed to that compactor. <see cref="AddContextCompaction"/> and <see cref="AddModelBackedContextCompaction"/> are
/// sugar that register the default compactor key with the extractive or the model-backed strategy. Composition requires
/// an <see cref="ISessionCoordinator"/> to already be registered (for example through <c>AddAgentSession</c>); no method
/// here registers one. The model-backed variant additionally requires the engine's <see cref="IModelCatalog"/>,
/// <see cref="IModelSelector"/>, and <see cref="ILlmModelResolver"/> (for example through the
/// <c>AgentKit.Providers</c> registration) plus at least one branded provider package supplying the selected model.
/// </para>
/// <para>
/// Every method validates its arguments before changing the collection, returns the same collection, and neither builds
/// nor resolves a service provider. Strategies, summary generators, and event sinks are additive by their own identity
/// under one compactor key; every other collaborator is singular per compactor key and has exactly one
/// <c>Replace*</c> method. Strategies, generators, and sinks accept the <see cref="ServiceLifetime.Singleton"/> and
/// <see cref="ServiceLifetime.Transient"/> lifetimes only, because the compactor and its resolvers are singletons and
/// must not capture a scoped service.
/// </para>
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the built-in extractive compaction pipeline under the default compactor key.
        /// </summary>
        /// <param name="configure">Optional configuration for <see cref="CompactionOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Idempotent: every registration here uses <c>TryAdd</c> semantics,
        /// so calling this more than once keeps the first registration, and
        /// calling it after <see cref="AddModelBackedContextCompaction"/>
        /// keeps the model-backed strategy as the default. Options are validated on first
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
            ArgumentNullException.ThrowIfNull(services);
            ContextCompactionRegistration.RegisterSharedInfrastructure(services);
            if (configure is not null)
            {
                _ = services.AddOptions<CompactionOptions>().Configure(configure);
            }

            return services.AddAgentContextCompaction(AgentContextCompactionComponentDefaults.CompactorKey);
        }

        /// <summary>Registers the built-in compaction pipeline under an explicit compactor key.</summary>
        /// <param name="key">The nondefault stable compactor key.</param>
        /// <param name="configure">Optional configuration for this compactor's <see cref="ContextCompactionOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is the default value.</exception>
        /// <remarks>
        /// <para>
        /// Registers the compactor, the extractive strategy under <see cref="CompactionStrategyKeys.Extractive"/>, and the
        /// keyed cut selector, validator, strategy resolver, summary-generator resolver, activation coordinator, and event
        /// dispatcher for <paramref name="key"/>, plus the engine-wide estimator, identifier generators, and
        /// <see cref="CompactionOptions"/> validation. Every registration is <c>TryAdd</c>, so repeating the call for the
        /// same key keeps the first collaborators; each call's <paramref name="configure"/> is still applied to the
        /// key's named options. Options are validated on first access: every ceiling must be positive (retained entries
        /// may be zero), the reduction ratio must lie strictly between 0 and 1, and
        /// <see cref="ContextCompactionOptions.DefaultStrategyOrder"/> must be a non-empty, duplicate-free list of
        /// strategies registered for this key.
        /// </para>
        /// <para>
        /// The default key is the one the engine and loop resolve unkeyed, so registering it also exposes the compactor
        /// unkeyed.
        /// </para>
        /// </remarks>
        public IServiceCollection AddAgentContextCompaction(
            ComponentKey<ICompactor> key,
            Action<ContextCompactionOptions>? configure = null) =>
            ContextCompactionRegistration.Add(services, key, configure);

        /// <summary>
        /// Registers the built-in compaction pipeline under the default compactor key with
        /// <see cref="ModelCompactionStrategy"/> as its default strategy, so checkpoints are model-generated summaries
        /// produced under <see cref="CompactionOptions.SummaryPrompt"/>.
        /// </summary>
        /// <param name="configure">
        /// Optional configuration for <see cref="CompactionOptions"/>. At minimum
        /// <see cref="CompactionOptions.SummaryModelPolicy"/> must be set, because the summary model is an external
        /// fact the application names; <see cref="CompactionOptions.SummaryPrompt"/> may be overridden here.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <para>
        /// This method first applies <see cref="AddContextCompaction"/> with the same <paramref name="configure"/>, then
        /// registers, with <c>TryAdd</c> semantics, <see cref="ModelBackedSummaryGenerator"/> and
        /// <see cref="ModelCompactionStrategy"/> under the default compactor key and makes
        /// <see cref="CompactionStrategyKeys.ModelSummary"/> that compactor's
        /// <see cref="ContextCompactionOptions.DefaultStrategyOrder"/>. Calling it more than once is idempotent; calling it
        /// after <see cref="AddContextCompaction"/> switches the already registered pipeline to the model-backed strategy
        /// without duplicating any other collaborator. A profile can still order the extractive strategy explicitly.
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
            ContextCompactionRegistration.RegisterModelBacked(services, AgentContextCompactionComponentDefaults.CompactorKey);
            return services;
        }

        /// <summary>Registers one named compaction profile that selects a compactor and orders its strategies.</summary>
        /// <param name="profile">The nondefault profile key that an agent definition's <see cref="AgentOptionalCapabilitySelection.CompactionProfile"/> names.</param>
        /// <param name="compactor">The nondefault key of the compactor the profile selects.</param>
        /// <param name="configure">Configures the profile; invoked once, immediately, on a fresh <see cref="CompactionProfileOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="configure"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="profile"/> or <paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="InvalidOperationException">
        /// The profile key is already registered, or the configured options carry a default version, an empty strategy
        /// order, a default key, or a duplicate strategy key.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A profile is one immutable publication, so registering the same key twice fails rather than merging; the
        /// configured values are captured at this call and later changes to the options instance have no effect. Checks
        /// that need other registrations run when the first <see cref="ICompactionProfileCatalog"/> lookup builds the
        /// catalog, which composition validation triggers for every selected profile: the compactor key must be
        /// registered, every ordered strategy must be registered for it, a strategy that requires a summary generator needs
        /// that generator registered for it, oversized-turn repair requires every ordered strategy to advertise it, and the
        /// order must respect each strategy's before/after constraints. The compiled policy takes its ceilings from the
        /// compactor's <see cref="ContextCompactionOptions"/>; a profile does not loosen them.
        /// </para>
        /// <para>
        /// This method registers the catalog with <c>TryAdd</c> semantics and does not require the compactor to be
        /// registered first.
        /// </para>
        /// </remarks>
        public IServiceCollection AddCompactionProfile(
            CompactionProfileKey profile,
            ComponentKey<ICompactor> compactor,
            Action<CompactionProfileOptions> configure) =>
            ContextCompactionRegistration.AddProfile(services, profile, compactor, configure);

        /// <summary>Replaces the compactor registered under <paramref name="key"/> with <typeparamref name="TCompactor"/>.</summary>
        /// <typeparam name="TCompactor">The compactor implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="key">The nondefault compactor key whose registration is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is the default value.</exception>
        /// <remarks>Replacing a key that was never registered simply registers it; replacing the default key keeps the unkeyed registration resolving to it. No other key's compactor changes.</remarks>
        public IServiceCollection ReplaceCompactor<TCompactor>(ComponentKey<ICompactor> key)
            where TCompactor : class, ICompactor =>
            ContextCompactionRegistration.ReplaceCompactor<TCompactor>(services, key);

        /// <summary>Registers one additional strategy for a compactor.</summary>
        /// <typeparam name="TStrategy">The strategy implementation, constructed by dependency injection.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor that owns the strategy.</param>
        /// <param name="registration">The strategy descriptor, ordering constraints, and lifetime; its descriptor key is the strategy's identity under the compactor.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="registration"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="ArgumentException"><paramref name="registration"/> names a lifetime other than singleton or transient.</exception>
        /// <exception cref="InvalidOperationException">The same strategy key is already registered for the compactor with a different implementation or registration; use <see cref="ReplaceCompactionStrategy"/>.</exception>
        /// <remarks>Registering an equivalent strategy again is an idempotent no-op. A profile selects registered strategies by key through <see cref="CompactionProfileOptions.StrategyOrder"/>.</remarks>
        public IServiceCollection AddCompactionStrategy<TStrategy>(
            ComponentKey<ICompactor> compactor,
            CompactionStrategyRegistration registration)
            where TStrategy : class, ICompactionStrategy =>
            ContextCompactionRegistration.AddStrategy<TStrategy>(services, compactor, registration);

        /// <summary>Replaces the strategy registered for a compactor under the registration's descriptor key.</summary>
        /// <typeparam name="TStrategy">The replacement implementation, constructed by dependency injection.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor that owns the strategy.</param>
        /// <param name="registration">The replacement descriptor, ordering constraints, and lifetime.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="registration"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="ArgumentException"><paramref name="registration"/> names a lifetime other than singleton or transient.</exception>
        /// <remarks>Replacing a strategy that was never registered registers it. No other strategy or compactor changes.</remarks>
        public IServiceCollection ReplaceCompactionStrategy<TStrategy>(
            ComponentKey<ICompactor> compactor,
            CompactionStrategyRegistration registration)
            where TStrategy : class, ICompactionStrategy =>
            ContextCompactionRegistration.ReplaceStrategy<TStrategy>(services, compactor, registration);

        /// <summary>Registers one additional summary generator for a compactor.</summary>
        /// <typeparam name="TGenerator">The generator implementation, constructed by dependency injection.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor that owns the generator.</param>
        /// <param name="registration">The generator descriptor and lifetime; its descriptor key is the generator's identity under the compactor.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="registration"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="ArgumentException"><paramref name="registration"/> names a lifetime other than singleton or transient.</exception>
        /// <exception cref="InvalidOperationException">The same generator key is already registered for the compactor with a different implementation or registration; use <see cref="ReplaceCompactionSummaryGenerator"/>.</exception>
        /// <remarks>Registering an equivalent generator again is an idempotent no-op.</remarks>
        public IServiceCollection AddCompactionSummaryGenerator<TGenerator>(
            ComponentKey<ICompactor> compactor,
            CompactionSummaryGeneratorRegistration registration)
            where TGenerator : class, ICompactionSummaryGenerator =>
            ContextCompactionRegistration.AddSummaryGenerator<TGenerator>(services, compactor, registration);

        /// <summary>Replaces the summary generator registered for a compactor under the registration's descriptor key.</summary>
        /// <typeparam name="TGenerator">The replacement implementation, constructed by dependency injection.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor that owns the generator.</param>
        /// <param name="registration">The replacement descriptor and lifetime.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="registration"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="ArgumentException"><paramref name="registration"/> names a lifetime other than singleton or transient.</exception>
        /// <remarks>Replacing a generator that was never registered registers it.</remarks>
        public IServiceCollection ReplaceCompactionSummaryGenerator<TGenerator>(
            ComponentKey<ICompactor> compactor,
            CompactionSummaryGeneratorRegistration registration)
            where TGenerator : class, ICompactionSummaryGenerator =>
            ContextCompactionRegistration.ReplaceSummaryGenerator<TGenerator>(services, compactor, registration);

        /// <summary>Replaces the summary-generator resolver of one compactor.</summary>
        /// <typeparam name="TResolver">The resolver implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose resolver is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        public IServiceCollection ReplaceCompactionSummaryGeneratorResolver<TResolver>(ComponentKey<ICompactor> compactor)
            where TResolver : class, ICompactionSummaryGeneratorResolver =>
            ContextCompactionRegistration.ReplaceSummaryGeneratorResolver<TResolver>(services, compactor);

        /// <summary>Replaces the cut selector of one compactor.</summary>
        /// <typeparam name="TSelector">The selector implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose selector is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <remarks>Only this compactor changes; compactors without their own selector keep the engine-wide default.</remarks>
        public IServiceCollection ReplaceCompactionCutSelector<TSelector>(ComponentKey<ICompactor> compactor)
            where TSelector : class, ICompactionCutSelector =>
            ContextCompactionRegistration.ReplaceCutSelector<TSelector>(services, compactor);

        /// <summary>Replaces the validator of one compactor.</summary>
        /// <typeparam name="TValidator">The validator implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose validator is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <remarks>Only this compactor changes; compactors without their own validator keep the engine-wide default.</remarks>
        public IServiceCollection ReplaceCompactionValidator<TValidator>(ComponentKey<ICompactor> compactor)
            where TValidator : class, ICompactionValidator =>
            ContextCompactionRegistration.ReplaceValidator<TValidator>(services, compactor);

        /// <summary>Replaces the strategy resolver of one compactor.</summary>
        /// <typeparam name="TResolver">The resolver implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose resolver is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        public IServiceCollection ReplaceCompactionStrategyResolver<TResolver>(ComponentKey<ICompactor> compactor)
            where TResolver : class, ICompactionStrategyResolver =>
            ContextCompactionRegistration.ReplaceStrategyResolver<TResolver>(services, compactor);

        /// <summary>Replaces the activation coordinator of one compactor.</summary>
        /// <typeparam name="TCoordinator">The coordinator implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose coordinator is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        public IServiceCollection ReplaceCompactionActivationCoordinator<TCoordinator>(ComponentKey<ICompactor> compactor)
            where TCoordinator : class, ICompactionActivationCoordinator =>
            ContextCompactionRegistration.ReplaceActivation<TCoordinator>(services, compactor);

        /// <summary>Registers one additional event sink for a compactor.</summary>
        /// <typeparam name="TSink">The sink implementation, constructed by dependency injection.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose events the sink receives.</param>
        /// <param name="registration">The sink identity, dispatch order, delivery semantics, and lifetime.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="registration"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="ArgumentException"><paramref name="registration"/> names a lifetime other than singleton or transient.</exception>
        /// <exception cref="InvalidOperationException">The same sink identity is already registered for the compactor with a different implementation or registration; use <see cref="ReplaceCompactionEventSink"/>.</exception>
        /// <remarks>
        /// Registering an equivalent sink again is an idempotent no-op. Sinks receive events in ascending
        /// <see cref="CompactionEventSinkRegistration.Order"/>, then by identity. A <see cref="CompactionEventDelivery.Required"/>
        /// sink that is unavailable or throws blocks the attempt with a typed failure; a best-effort sink never does.
        /// </remarks>
        public IServiceCollection AddCompactionEventSink<TSink>(
            ComponentKey<ICompactor> compactor,
            CompactionEventSinkRegistration registration)
            where TSink : class, ICompactionEventSink =>
            ContextCompactionRegistration.AddEventSink<TSink>(services, compactor, registration);

        /// <summary>Replaces the event sink registered for a compactor under the registration's identity.</summary>
        /// <typeparam name="TSink">The replacement implementation, constructed by dependency injection.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose events the sink receives.</param>
        /// <param name="registration">The replacement identity, order, delivery semantics, and lifetime.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> or <paramref name="registration"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <exception cref="ArgumentException"><paramref name="registration"/> names a lifetime other than singleton or transient.</exception>
        /// <remarks>Replacing a sink that was never registered registers it.</remarks>
        public IServiceCollection ReplaceCompactionEventSink<TSink>(
            ComponentKey<ICompactor> compactor,
            CompactionEventSinkRegistration registration)
            where TSink : class, ICompactionEventSink =>
            ContextCompactionRegistration.ReplaceEventSink<TSink>(services, compactor, registration);

        /// <summary>Replaces the event dispatcher of one compactor.</summary>
        /// <typeparam name="TDispatcher">The dispatcher implementation, constructed by dependency injection as a singleton.</typeparam>
        /// <param name="compactor">The nondefault key of the compactor whose dispatcher is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><c>services</c> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="compactor"/> is the default value.</exception>
        /// <remarks>A replacement dispatcher takes over delivery entirely; registered sinks reach it only if it resolves them itself.</remarks>
        public IServiceCollection ReplaceCompactionEventDispatcher<TDispatcher>(ComponentKey<ICompactor> compactor)
            where TDispatcher : class, ICompactionEventDispatcher =>
            ContextCompactionRegistration.ReplaceEventDispatcher<TDispatcher>(services, compactor);
    }
}
