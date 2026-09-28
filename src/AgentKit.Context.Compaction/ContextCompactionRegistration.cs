// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Registers keyed compaction services.</summary>
internal static class ContextCompactionRegistration
{
    internal static IServiceCollection Add(
        IServiceCollection services,
        ComponentKey<ICompactor> compactorKey,
        Action<ContextCompactionOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentOutOfRangeException.ThrowIfEqual(compactorKey, default);

        _ = RegisterSharedInfrastructure(services, configure, compactorKey);

        var strategyResolverKey = CompactionServiceKeys.StrategyResolver(compactorKey);
        services.TryAddKeyedSingleton<ICompactionStrategyResolver>(
            strategyResolverKey,
            (provider, _) => new DefaultCompactionStrategyResolver(compactorKey, provider));

        var generatorResolverKey = CompactionServiceKeys.SummaryGeneratorResolver(compactorKey);
        services.TryAddKeyedSingleton<ICompactionSummaryGeneratorResolver>(
            generatorResolverKey,
            (provider, _) => new DefaultSummaryGeneratorResolver(compactorKey, provider));

        var eventDispatcherKey = CompactionServiceKeys.EventDispatcher(compactorKey);
        services.TryAddKeyedSingleton<ICompactionEventDispatcher>(
            eventDispatcherKey,
            (provider, _) =>
            {
                var declarations = provider.GetServices<CompactionEventSinkDeclaration>();
                return new DefaultCompactionEventDispatcher(compactorKey, declarations, provider);
            });

        var activationKey = CompactionServiceKeys.ActivationCoordinator(compactorKey);
        services.TryAddKeyedSingleton<ICompactionActivationCoordinator>(
            activationKey,
            (provider, _) => new SessionCompactionActivationCoordinator(
                provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<CompactionOptions>>(),
                provider.GetService<IDurableExecutionCoordinator>(),
                provider.GetService<IDurabilityProfileCatalog>(),
                provider.GetRequiredService<DurableBoundaryRegistry>()));

        services.TryAddKeyedSingleton<ICompactionStrategy>(
            CompactionServiceKeys.Strategy(compactorKey, ExtractiveCompactionStrategy.StrategyKey),
            (provider, _) => provider.GetRequiredService<ExtractiveCompactionStrategy>());

        services.TryAddKeyedSingleton<ICompactor>(
            compactorKey.Value,
            (provider, _) => CreateDefaultCompactor(provider, compactorKey));

        if (compactorKey.Equals(AgentContextCompactionComponentDefaults.CompactorKey))
        {
            services.TryAddSingleton(static provider =>
                provider.GetRequiredKeyedService<ICompactor>(AgentContextCompactionComponentDefaults.CompactorKey.Value));
        }

        return services;
    }

    private static DefaultCompactor CreateDefaultCompactor(IServiceProvider provider, ComponentKey<ICompactor> compactorKey) =>
        new(
            provider.GetRequiredService<ISessionCoordinator>(),
            provider.GetRequiredService<ICompactionCutSelector>(),
            provider.GetRequiredKeyedService<ICompactionStrategyResolver>(
                CompactionServiceKeys.StrategyResolver(compactorKey)),
            provider.GetRequiredService<ICompactionValidator>(),
            provider.GetRequiredKeyedService<ICompactionActivationCoordinator>(
                CompactionServiceKeys.ActivationCoordinator(compactorKey)),
            provider.GetRequiredKeyedService<ICompactionEventDispatcher>(
                CompactionServiceKeys.EventDispatcher(compactorKey)),
            provider.GetRequiredService<ICompactionSizeEstimator>(),
            provider.GetRequiredService<IIdentifierGenerator<CompactionManifestId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IOptions<CompactionOptions>>(),
            provider.GetRequiredService<ContextCompactionOptionsSnapshot>(),
            provider.GetRequiredService<ILogger<DefaultCompactor>>());

    internal static IServiceCollection RegisterSharedInfrastructure(
        IServiceCollection services,
        Action<ContextCompactionOptions>? configure,
        ComponentKey<ICompactor> compactorKey)
    {
        var optionsBuilder = services.AddOptions<ContextCompactionOptions>();
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(provider =>
            CreateSnapshot(compactorKey, provider.GetRequiredService<IOptions<ContextCompactionOptions>>().Value));
        RegisterDurableBoundaries(services);
        return services;
    }

    /// <summary>Registers the bridge that lets the durability coordinator drive compaction's journaled boundary.</summary>
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
            options.PersistRejectedCandidates);
}
