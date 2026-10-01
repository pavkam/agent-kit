// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers keyed compaction services.</summary>
/// <remarks>
/// Every method here validates its arguments before touching the collection, never builds or resolves a provider, and
/// registers only values and factories that read their collaborators lazily. Collaborators are keyed by the exact
/// compactor key text (plus the strategy, generator, or sink identity where the collaborator is additive), so two
/// compactors registered in one collection never share or overwrite each other's registrations.
/// </remarks>
internal static class ContextCompactionRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        ComponentKey<ICompactor> compactorKey,
        Action<ContextCompactionOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);

        RegisterSharedInfrastructure(services);
        RegisterCompactorOptions(services, compactorKey, configure);
        RegisterDurableBoundaries(services);

        services.TryAddKeyedSingleton<ICompactionStrategyResolver>(
            CompactionServiceKeys.StrategyResolver(compactorKey),
            (provider, _) => new DefaultCompactionStrategyResolver(compactorKey, provider));
        services.TryAddKeyedSingleton<ICompactionSummaryGeneratorResolver>(
            CompactionServiceKeys.SummaryGeneratorResolver(compactorKey),
            (provider, _) => new DefaultSummaryGeneratorResolver(compactorKey, provider));
        services.TryAddKeyedSingleton<ICompactionEventDispatcher>(
            CompactionServiceKeys.EventDispatcher(compactorKey),
            (provider, _) => new DefaultCompactionEventDispatcher(
                compactorKey,
                provider.GetServices<CompactionEventSinkDeclaration>(),
                provider));
        services.TryAddKeyedSingleton<ICompactionActivationCoordinator>(
            CompactionServiceKeys.ActivationCoordinator(compactorKey),
            (provider, _) => new SessionCompactionActivationCoordinator(
                provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<CompactionOptions>>(),
                provider.GetService<IDurableExecutionCoordinator>(),
                provider.GetService<IDurabilityProfileCatalog>(),
                provider.GetRequiredService<DurableBoundaryRegistry>()));

        // The per-key selector and validator default to the engine-wide unkeyed defaults, so replacing the unkeyed
        // registration still reaches every compactor that has not been given its own.
        services.TryAddKeyedSingleton(
            CompactionServiceKeys.CutSelector(compactorKey),
            static (provider, _) => provider.GetRequiredService<ICompactionCutSelector>());
        services.TryAddKeyedSingleton(
            CompactionServiceKeys.Validator(compactorKey),
            static (provider, _) => provider.GetRequiredService<ICompactionValidator>());

        RegisterStrategy(
            services,
            compactorKey,
            new CompactionStrategyRegistration(
                ExtractiveCompactionStrategy.DefaultDescriptor, order: 0, before: [], after: [], ServiceLifetime.Singleton),
            typeof(ExtractiveCompactionStrategy),
            factory: null,
            RegistrationMode.TryAdd);

        services.TryAddKeyedSingleton<ICompactor>(
            compactorKey.Value,
            (provider, _) => CreateDefaultCompactor(provider, compactorKey));
        RegisterDefaultKeyForwarder(services, compactorKey);

        return services;
    }

    /// <summary>Registers the model-backed strategy and generator under one compactor and makes it that compactor's default.</summary>
    /// <param name="services">The collection that already carries the compactor registration.</param>
    /// <param name="compactorKey">The compactor to switch to the model-backed strategy.</param>
    internal static void RegisterModelBacked(IServiceCollection services, ComponentKey<ICompactor> compactorKey)
    {
        Debug.Assert(services is not null, "The public entry point validates the collection.");
        Debug.Assert(compactorKey != default, "The public entry point validates the key.");

        RegisterSummaryGenerator(
            services,
            compactorKey,
            new CompactionSummaryGeneratorRegistration(ModelBackedSummaryGenerator.DefaultDescriptor, ServiceLifetime.Singleton),
            typeof(ModelBackedSummaryGenerator),
            RegistrationMode.TryAdd);
        RegisterStrategy(
            services,
            compactorKey,
            new CompactionStrategyRegistration(
                ModelCompactionStrategy.DefaultDescriptor, order: 10, before: [], after: [], ServiceLifetime.Singleton),
            typeof(ModelCompactionStrategy),
            static (provider, compactor) =>
            {
                // Resolving the generator first surfaces a missing model runtime as a composition failure here rather
                // than as a thrown exception in the middle of a compaction attempt.
                _ = provider.GetRequiredKeyedService<ICompactionSummaryGenerator>(
                    CompactionServiceKeys.SummaryGenerator(compactor, ModelBackedSummaryGenerator.GeneratorKey));
                return new ModelCompactionStrategy(
                    compactor,
                    provider.GetRequiredKeyedService<ICompactionSummaryGeneratorResolver>(
                        CompactionServiceKeys.SummaryGeneratorResolver(compactor)),
                    provider.GetRequiredService<ICompactionSizeEstimator>(),
                    provider.GetRequiredService<IIdentifierGenerator<MessageId>>(),
                    provider.GetRequiredService<TimeProvider>(),
                    provider.GetRequiredService<IOptions<CompactionOptions>>());
            },
            RegistrationMode.TryAdd);
        _ = services.AddOptions<ContextCompactionOptions>(compactorKey.Value)
            .Configure(static options => options.DefaultStrategyOrder = [CompactionStrategyKeys.ModelSummary]);
    }

    internal static IServiceCollection AddProfile(
        IServiceCollection services,
        CompactionProfileKey profile,
        ComponentKey<ICompactor> compactor,
        Action<CompactionProfileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(profile, default);
        ArgumentOutOfRangeException.ThrowIfEqual(compactor, default);
        ArgumentNullException.ThrowIfNull(configure);

        if (services.Any(descriptor => descriptor.ImplementationInstance is CompactionProfileDeclaration existing && existing.Key == profile))
        {
            throw new InvalidOperationException(
                $"Compaction profile '{profile.Value}' is already registered. A profile is one immutable publication; register each key once.");
        }

        var options = new CompactionProfileOptions();
        configure(options);
        var declaration = Capture(profile, compactor, options);

        services.TryAddSingleton<ICompactionProfileCatalog>(static provider => CompactionProfileRegistry.Build(provider));
        _ = services.AddSingleton(declaration);
        return services;
    }

    internal static IServiceCollection ReplaceCompactor<TCompactor>(IServiceCollection services, ComponentKey<ICompactor> key)
        where TCompactor : class, ICompactor
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);

        ReplaceKeyed<ICompactor, TCompactor>(services, key.Value);
        RegisterDefaultKeyForwarder(services, key);
        return services;
    }

    internal static IServiceCollection AddStrategy<TStrategy>(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionStrategyRegistration registration)
        where TStrategy : class, ICompactionStrategy
    {
        ValidateStrategyArguments(services, compactor, registration);
        RegisterStrategy(services, compactor, registration, typeof(TStrategy), factory: null, RegistrationMode.Add);
        return services;
    }

    internal static IServiceCollection ReplaceStrategy<TStrategy>(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionStrategyRegistration registration)
        where TStrategy : class, ICompactionStrategy
    {
        ValidateStrategyArguments(services, compactor, registration);
        RegisterStrategy(services, compactor, registration, typeof(TStrategy), factory: null, RegistrationMode.Replace);
        return services;
    }

    internal static IServiceCollection AddSummaryGenerator<TGenerator>(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionSummaryGeneratorRegistration registration)
        where TGenerator : class, ICompactionSummaryGenerator
    {
        ValidateGeneratorArguments(services, compactor, registration);
        RegisterSummaryGenerator(services, compactor, registration, typeof(TGenerator), RegistrationMode.Add);
        return services;
    }

    internal static IServiceCollection ReplaceSummaryGenerator<TGenerator>(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionSummaryGeneratorRegistration registration)
        where TGenerator : class, ICompactionSummaryGenerator
    {
        ValidateGeneratorArguments(services, compactor, registration);
        RegisterSummaryGenerator(services, compactor, registration, typeof(TGenerator), RegistrationMode.Replace);
        return services;
    }

    internal static IServiceCollection ReplaceSummaryGeneratorResolver<TResolver>(
        IServiceCollection services, ComponentKey<ICompactor> compactor)
        where TResolver : class, ICompactionSummaryGeneratorResolver =>
        ReplaceSingular<ICompactionSummaryGeneratorResolver, TResolver>(
            services, compactor, CompactionServiceKeys.SummaryGeneratorResolver);

    internal static IServiceCollection ReplaceCutSelector<TSelector>(IServiceCollection services, ComponentKey<ICompactor> compactor)
        where TSelector : class, ICompactionCutSelector =>
        ReplaceSingular<ICompactionCutSelector, TSelector>(services, compactor, CompactionServiceKeys.CutSelector);

    internal static IServiceCollection ReplaceValidator<TValidator>(IServiceCollection services, ComponentKey<ICompactor> compactor)
        where TValidator : class, ICompactionValidator =>
        ReplaceSingular<ICompactionValidator, TValidator>(services, compactor, CompactionServiceKeys.Validator);

    internal static IServiceCollection ReplaceStrategyResolver<TResolver>(IServiceCollection services, ComponentKey<ICompactor> compactor)
        where TResolver : class, ICompactionStrategyResolver =>
        ReplaceSingular<ICompactionStrategyResolver, TResolver>(services, compactor, CompactionServiceKeys.StrategyResolver);

    internal static IServiceCollection ReplaceActivation<TCoordinator>(IServiceCollection services, ComponentKey<ICompactor> compactor)
        where TCoordinator : class, ICompactionActivationCoordinator =>
        ReplaceSingular<ICompactionActivationCoordinator, TCoordinator>(
            services, compactor, CompactionServiceKeys.ActivationCoordinator);

    internal static IServiceCollection ReplaceEventDispatcher<TDispatcher>(IServiceCollection services, ComponentKey<ICompactor> compactor)
        where TDispatcher : class, ICompactionEventDispatcher =>
        ReplaceSingular<ICompactionEventDispatcher, TDispatcher>(services, compactor, CompactionServiceKeys.EventDispatcher);

    internal static IServiceCollection AddEventSink<TSink>(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionEventSinkRegistration registration)
        where TSink : class, ICompactionEventSink
    {
        ValidateSinkArguments(services, compactor, registration);
        RegisterEventSink(services, compactor, registration, typeof(TSink), RegistrationMode.Add);
        return services;
    }

    internal static IServiceCollection ReplaceEventSink<TSink>(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionEventSinkRegistration registration)
        where TSink : class, ICompactionEventSink
    {
        ValidateSinkArguments(services, compactor, registration);
        RegisterEventSink(services, compactor, registration, typeof(TSink), RegistrationMode.Replace);
        return services;
    }

    /// <summary>Registers the engine-wide collaborators every compactor shares, once, with <c>TryAdd</c> semantics.</summary>
    internal static void RegisterSharedInfrastructure(IServiceCollection services)
    {
        var first = !services.Any(static descriptor => descriptor.ServiceType == typeof(CompactionSharedInfrastructureMarker));
        services.TryAddSingleton<CompactionSharedInfrastructureMarker>();
        _ = services.AddAgentKitObservability();
        var optionsBuilder = services.AddOptions<CompactionOptions>();
        if (first)
        {
            _ = optionsBuilder
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
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<CompactionManifestId>>(
            _ => new GuidIdentifierGenerator<CompactionManifestId>(static value => new CompactionManifestId(value)));
        services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>>(
            _ => new GuidIdentifierGenerator<SessionEntryId>(static value => new SessionEntryId(value)));
        services.TryAddSingleton<IIdentifierGenerator<MessageId>>(
            _ => new GuidIdentifierGenerator<MessageId>(static value => new MessageId(value)));
        services.TryAddSingleton<IIdentifierGenerator<ModelRequestId>>(
            _ => new GuidIdentifierGenerator<ModelRequestId>(static value => new ModelRequestId(value)));
        services.TryAddSingleton<ICompactionSizeEstimator, CharacterCompactionSizeEstimator>();
        services.TryAddSingleton<ICompactionCutSelector, StructuralCompactionCutSelector>();
        services.TryAddSingleton<ICompactionValidator, DefaultCompactionValidator>();
    }

    /// <summary>Binds the per-compactor options as named options and publishes the immutable snapshot under the key.</summary>
    private static void RegisterCompactorOptions(
        IServiceCollection services,
        ComponentKey<ICompactor> compactorKey,
        Action<ContextCompactionOptions>? configure)
    {
        var first = !services.Any(descriptor =>
            descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(ContextCompactionOptionsSnapshot)
            && compactorKey.Value.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal));
        var optionsBuilder = services.AddOptions<ContextCompactionOptions>(compactorKey.Value);
        if (first)
        {
            _ = optionsBuilder
                .Validate(static o => o.MaximumAttempts > 0, "MaximumAttempts must be positive.")
                .Validate(static o => o.MaximumSourceEntries > 0, "MaximumSourceEntries must be positive.")
                .Validate(static o => o.MaximumSourceBytes > 0, "MaximumSourceBytes must be positive.")
                .Validate(static o => o.MaximumSummaryTokens > 0, "MaximumSummaryTokens must be positive.")
                .Validate(static o => o.MinimumRetainedEntries >= 0, "MinimumRetainedEntries must not be negative.")
                .Validate(static o => o.MaximumValidationIssues > 0, "MaximumValidationIssues must be positive.")
                .Validate(
                    static o => o.MinimumReductionRatio is > 0d and < 1d,
                    "MinimumReductionRatio must be greater than 0 and less than 1.")
                .Validate(static o => o.AttemptTimeout > TimeSpan.Zero, "AttemptTimeout must be positive.")
                .Validate(
                    static o => o.DefaultStrategyOrder is { Count: > 0 }
                        && !o.DefaultStrategyOrder.Contains(default)
                        && o.DefaultStrategyOrder.Distinct().Count() == o.DefaultStrategyOrder.Count,
                    "DefaultStrategyOrder must name at least one strategy, without default or duplicate keys.");
        }

        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddKeyedSingleton(
            compactorKey.Value,
            (provider, _) => CreateSnapshot(
                compactorKey,
                provider.GetRequiredService<IOptionsMonitor<ContextCompactionOptions>>().Get(compactorKey.Value)));
    }

    private static DefaultCompactor CreateDefaultCompactor(IServiceProvider provider, ComponentKey<ICompactor> compactorKey)
    {
        var snapshot = provider.GetRequiredKeyedService<ContextCompactionOptionsSnapshot>(compactorKey.Value);
        var registered = provider.GetServices<CompactionStrategyDeclaration>()
            .Where(strategy => strategy.CompactorKey.Equals(compactorKey.Value, StringComparison.Ordinal))
            .Select(static strategy => strategy.Registration.Descriptor.Key)
            .ToHashSet();
        foreach (var strategyKey in snapshot.DefaultStrategyOrder.Where(key => !registered.Contains(key)))
        {
            throw new InvalidOperationException(
                $"Compactor '{compactorKey.Value}' orders default strategy '{strategyKey.Value}', which is not registered for it.");
        }

        return new DefaultCompactor(
            provider.GetRequiredService<ISessionCoordinator>(),
            provider.GetRequiredKeyedService<ICompactionCutSelector>(CompactionServiceKeys.CutSelector(compactorKey)),
            provider.GetRequiredKeyedService<ICompactionStrategyResolver>(
                CompactionServiceKeys.StrategyResolver(compactorKey)),
            provider.GetRequiredKeyedService<ICompactionValidator>(CompactionServiceKeys.Validator(compactorKey)),
            provider.GetRequiredKeyedService<ICompactionActivationCoordinator>(
                CompactionServiceKeys.ActivationCoordinator(compactorKey)),
            provider.GetRequiredKeyedService<ICompactionEventDispatcher>(
                CompactionServiceKeys.EventDispatcher(compactorKey)),
            provider.GetRequiredService<ICompactionSizeEstimator>(),
            provider.GetRequiredService<IIdentifierGenerator<CompactionManifestId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IOptions<CompactionOptions>>(),
            snapshot,
            provider.GetRequiredService<ILogger<DefaultCompactor>>());
    }

    /// <summary>Registers the registry and handler that let the durability coordinator drive compaction's journaled boundary.</summary>
    /// <param name="services">The service collection to register into.</param>
    /// <remarks>
    /// The registry and handler are registered unconditionally because they are inert without a composed durability
    /// runtime: no coordinator means nothing resolves them, and a profile that does not enable
    /// <see cref="CompactionDurableOperations.Activation"/> means nothing publishes a continuation into them. Every
    /// registration is <c>TryAdd</c>, so the registry stays the one engine-wide instance whichever package registers
    /// it first.
    /// </remarks>
    private static void RegisterDurableBoundaries(IServiceCollection services)
    {
        services.TryAddSingleton<DurableBoundaryRegistry>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IDurableOperationHandler, CompactionActivationDurableOperationHandler>());
    }

    /// <summary>Exposes the default compactor key's registration unkeyed, which is where the loop and engine look first.</summary>
    private static void RegisterDefaultKeyForwarder(IServiceCollection services, ComponentKey<ICompactor> compactorKey)
    {
        if (compactorKey.Equals(AgentContextCompactionComponentDefaults.CompactorKey))
        {
            services.TryAddSingleton(static provider =>
                provider.GetRequiredKeyedService<ICompactor>(AgentContextCompactionComponentDefaults.CompactorKey.Value));
        }
    }

    private static CompactionProfileDeclaration Capture(
        CompactionProfileKey profile,
        ComponentKey<ICompactor> compactor,
        CompactionProfileOptions options)
    {
        var problem = options switch
        {
            { Version.Value: 0 } => "must set a positive version",
            { StrategyOrder: null or { Count: 0 } } => "must order at least one strategy",
            { StrategyOrder: var order } when order.Contains(default) => "orders a default strategy key",
            { StrategyOrder: var order } when order.Distinct().Count() != order.Count => "orders a strategy more than once",
            _ => null,
        };
        return problem is null
            ? new CompactionProfileDeclaration(
                profile, compactor, options.Version, options.Enabled, [.. options.StrategyOrder!], options.AllowOversizedTurnRepair)
            : throw new InvalidOperationException($"Compaction profile '{profile.Value}' {problem}.");
    }

    private static ContextCompactionOptionsSnapshot CreateSnapshot(
        ComponentKey<ICompactor> compactorKey,
        ContextCompactionOptions options) =>
        new(
            compactorKey,
            options.MaximumAttempts,
            options.MaximumSourceEntries,
            options.MaximumSourceBytes,
            options.MaximumSummaryTokens,
            options.MinimumRetainedEntries,
            options.MaximumValidationIssues,
            options.MinimumReductionRatio,
            options.AttemptTimeout,
            options.PersistRejectedCandidates,
            [.. options.DefaultStrategyOrder]);

    private static void ValidateStrategyArguments(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionStrategyRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(compactor, default);
        ArgumentNullException.ThrowIfNull(registration);
        ThrowIfUnsupportedLifetime(registration.Lifetime, nameof(registration));
    }

    private static void ValidateGeneratorArguments(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionSummaryGeneratorRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(compactor, default);
        ArgumentNullException.ThrowIfNull(registration);
        ThrowIfUnsupportedLifetime(registration.Lifetime, nameof(registration));
    }

    private static void ValidateSinkArguments(
        IServiceCollection services, ComponentKey<ICompactor> compactor, CompactionEventSinkRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(compactor, default);
        ArgumentNullException.ThrowIfNull(registration);
        ThrowIfUnsupportedLifetime(registration.Lifetime, nameof(registration));
    }

    /// <summary>Rejects a lifetime the singleton compactor and resolvers cannot honor.</summary>
    /// <exception cref="ArgumentException">The lifetime is scoped or undefined.</exception>
    private static void ThrowIfUnsupportedLifetime(ServiceLifetime lifetime, string paramName)
    {
        if (lifetime is not (ServiceLifetime.Singleton or ServiceLifetime.Transient))
        {
            throw new ArgumentException(
                "Only Singleton and Transient lifetimes are supported: the compactor, resolvers, and dispatcher are singletons and must not capture a scoped service.",
                paramName);
        }
    }

    private static void RegisterStrategy(
        IServiceCollection services,
        ComponentKey<ICompactor> compactor,
        CompactionStrategyRegistration registration,
        Type implementationType,
        Func<IServiceProvider, ComponentKey<ICompactor>, ICompactionStrategy>? factory,
        RegistrationMode mode)
    {
        var key = registration.Descriptor.Key;
        var existing = services
            .Select(static descriptor => descriptor.ImplementationInstance as CompactionStrategyDeclaration)
            .FirstOrDefault(declaration =>
                declaration is not null
                && declaration.CompactorKey.Equals(compactor.Value, StringComparison.Ordinal)
                && declaration.Registration.Descriptor.Key == key);
        if (existing is not null && mode != RegistrationMode.Replace)
        {
            if (mode == RegistrationMode.TryAdd
                || (existing.ImplementationType == implementationType
                    && CompactionRegistrationEquality.Equivalent(existing.Registration, registration)))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Compaction strategy '{key.Value}' is already registered for compactor '{compactor.Value}' with a different implementation or registration. Use ReplaceCompactionStrategy to replace it.");
        }

        var serviceKey = CompactionServiceKeys.Strategy(compactor, key);
        RemoveKeyed(services, typeof(ICompactionStrategy), serviceKey);
        RemoveDeclaration(services, existing);
        services.Add(
            factory is null
                ? new ServiceDescriptor(typeof(ICompactionStrategy), serviceKey, implementationType, registration.Lifetime)
                : new ServiceDescriptor(
                    typeof(ICompactionStrategy),
                    serviceKey,
                    (provider, _) => factory(provider, compactor),
                    registration.Lifetime));
        _ = services.AddSingleton(new CompactionStrategyDeclaration(compactor.Value, registration, implementationType));
    }

    private static void RegisterSummaryGenerator(
        IServiceCollection services,
        ComponentKey<ICompactor> compactor,
        CompactionSummaryGeneratorRegistration registration,
        Type implementationType,
        RegistrationMode mode)
    {
        var key = registration.Descriptor.Key;
        var existing = services
            .Select(static descriptor => descriptor.ImplementationInstance as CompactionSummaryGeneratorDeclaration)
            .FirstOrDefault(declaration =>
                declaration is not null
                && declaration.CompactorKey.Equals(compactor.Value, StringComparison.Ordinal)
                && declaration.Registration.Descriptor.Key == key);
        if (existing is not null && mode != RegistrationMode.Replace)
        {
            if (mode == RegistrationMode.TryAdd
                || (existing.ImplementationType == implementationType && existing.Registration == registration))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Compaction summary generator '{key.Value}' is already registered for compactor '{compactor.Value}' with a different implementation or registration. Use ReplaceCompactionSummaryGenerator to replace it.");
        }

        var serviceKey = CompactionServiceKeys.SummaryGenerator(compactor, key);
        RemoveKeyed(services, typeof(ICompactionSummaryGenerator), serviceKey);
        RemoveDeclaration(services, existing);
        services.Add(new ServiceDescriptor(typeof(ICompactionSummaryGenerator), serviceKey, implementationType, registration.Lifetime));
        _ = services.AddSingleton(new CompactionSummaryGeneratorDeclaration(compactor.Value, registration, implementationType));
    }

    private static void RegisterEventSink(
        IServiceCollection services,
        ComponentKey<ICompactor> compactor,
        CompactionEventSinkRegistration registration,
        Type implementationType,
        RegistrationMode mode)
    {
        var id = registration.Id;
        var existing = services
            .Select(static descriptor => descriptor.ImplementationInstance as CompactionEventSinkDeclaration)
            .FirstOrDefault(declaration =>
                declaration is not null
                && declaration.CompactorKey.Equals(compactor.Value, StringComparison.Ordinal)
                && declaration.Registration.Id == id);
        if (existing is not null && mode == RegistrationMode.Add)
        {
            if (existing.SinkType == implementationType && existing.Registration == registration)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Compaction event sink '{id.Value}' is already registered for compactor '{compactor.Value}' with a different implementation or registration. Use ReplaceCompactionEventSink to replace it.");
        }

        var serviceKey = CompactionServiceKeys.EventSink(compactor, id);
        RemoveKeyed(services, typeof(ICompactionEventSink), serviceKey);
        RemoveDeclaration(services, existing);
        services.Add(new ServiceDescriptor(typeof(ICompactionEventSink), serviceKey, implementationType, registration.Lifetime));
        _ = services.AddSingleton(new CompactionEventSinkDeclaration(compactor.Value, registration, implementationType));
    }

    private static IServiceCollection ReplaceSingular<TService, TImplementation>(
        IServiceCollection services,
        ComponentKey<ICompactor> compactor,
        Func<ComponentKey<ICompactor>, string> serviceKey)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(compactor, default);
        ReplaceKeyed<TService, TImplementation>(services, serviceKey(compactor));
        return services;
    }

    private static void ReplaceKeyed<TService, TImplementation>(IServiceCollection services, string serviceKey)
        where TService : class
        where TImplementation : class, TService
    {
        RemoveKeyed(services, typeof(TService), serviceKey);
        services.Add(new ServiceDescriptor(typeof(TService), serviceKey, typeof(TImplementation), ServiceLifetime.Singleton));
    }

    private static void RemoveDeclaration(IServiceCollection services, object? declaration)
    {
        if (declaration is null)
        {
            return;
        }

        for (var index = services.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(services[index].ImplementationInstance, declaration))
            {
                services.RemoveAt(index);
            }
        }
    }

    private static void RemoveKeyed(IServiceCollection services, Type serviceType, string serviceKey)
    {
        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.IsKeyedService
                && descriptor.ServiceType == serviceType
                && serviceKey.Equals(descriptor.ServiceKey as string, StringComparison.Ordinal))
            {
                services.RemoveAt(index);
            }
        }
    }

    /// <summary>How a registration treats an existing declaration for the same identity.</summary>
    private enum RegistrationMode
    {
        /// <summary>Keep an existing declaration silently; used for first-party defaults.</summary>
        TryAdd,

        /// <summary>Accept an equivalent repeat and reject a conflicting duplicate.</summary>
        Add,

        /// <summary>Remove the existing declaration and register the new one.</summary>
        Replace,
    }
}
