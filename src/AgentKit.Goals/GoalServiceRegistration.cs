// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Performs goal, delegation, and join registrations without building or resolving a service provider.</summary>
/// <remarks>Additive registrations that name an existing key or identity with the same implementation are idempotent; naming it with a different implementation fails immediately, because duplicate keys and identities are configuration errors unless an explicit replacement names the registration. Singular axes are registered with <c>TryAdd</c> and replaced only through explicit replacement methods.</remarks>
internal static class GoalServiceRegistration
{
    internal static IServiceCollection AddAgentGoals(IServiceCollection services, Action<AgentGoalOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentKitObservability();
        var optionsBuilder = services.AddOptions<AgentGoalOptions>()
            .Validate(static options => options.MaximumDelegationDepth > 0, "MaximumDelegationDepth must be positive.")
            .Validate(static options => options.MaximumChildrenPerGoal > 0, "MaximumChildrenPerGoal must be positive.")
            .Validate(static options => options.MaximumConcurrentAttempts > 0, "MaximumConcurrentAttempts must be positive.")
            .Validate(static options => Enum.IsDefined(options.FailureMode), "FailureMode must be a defined value.")
            .Validate(static options => !string.IsNullOrWhiteSpace(options.DefaultJoinStrategy.Value), "DefaultJoinStrategy must be set.")
            .Validate(static options => options.JoinPollInterval > TimeSpan.Zero, "JoinPollInterval must be positive.")
            .Validate(static options => options.GrantLifetime > TimeSpan.Zero, "GrantLifetime must be positive.")
            .Validate(static options => options.MaximumResultSummaryCharacters > 0, "MaximumResultSummaryCharacters must be positive.")
            .ValidateOnStart();
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IIdentifierGenerator<GoalAttemptId>, GuidGoalAttemptIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
        services.TryAddSingleton(static provider =>
        {
            var registry = new GoalProfileRegistry(provider.GetRequiredService<IOptions<AgentGoalOptions>>().Value);
            foreach (var contributor in provider.GetServices<IGoalProfileContributor>())
            {
                contributor.Contribute(registry);
            }

            return registry;
        });
        services.TryAddSingleton<IGoalProfileCatalog, InMemoryGoalProfileCatalog>();
        services.TryAddSingleton<IGoalStoreSelector, DefaultGoalStoreSelector>();
        services.TryAddSingleton<IDelegationDispatcherSelector, DefaultDelegationDispatcherSelector>();
        services.TryAddSingleton<IGoalJoinStrategySelector, DefaultGoalJoinStrategySelector>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDelegationTargetProvider, LocalAgentDelegationTargetProvider>());
        services.TryAddSingleton<IDelegationTargetCatalog, DefaultDelegationTargetCatalog>();
        services.TryAddSingleton<IDelegationTargetSelector, DefaultDelegationTargetSelector>();
        services.TryAddSingleton<IDelegationPolicyPipeline, DefaultDelegationPolicyPipeline>();
        services.TryAddSingleton(static provider => new DefaultGoalBudgetManager(provider.GetService<IBudgetAuthority>()));
        services.TryAddSingleton<IGoalBudgetManager>(static provider => provider.GetRequiredService<DefaultGoalBudgetManager>());
        services.TryAddSingleton<IGoalEventDispatcher, DefaultGoalEventDispatcher>();
        services.TryAddSingleton<IDelegationWaitParking, NoOpDelegationWaitParking>();
        services.TryAddSingleton<IDelegationIntentSignal, NoOpDelegationIntentSignal>();
        services.TryAddSingleton<GoalGrantIssuer>();
        services.TryAddSingleton<IGoalCoordinator, DefaultGoalCoordinator>();
        services.TryAddSingleton<IDelegationCoordinator, DelegationCoordinator>();
        if (!services.Any(static descriptor => descriptor.ServiceType == typeof(DelegationPolicyDeclaration)
                && descriptor.ImplementationInstance is DelegationPolicyDeclaration { Registration.Id: var id }
                && id == DenyUnlessAuthorizedDelegationPolicy.PolicyId))
        {
            services.TryAddSingleton<DenyUnlessAuthorizedDelegationPolicy>();
            _ = services.AddSingleton(new DelegationPolicyDeclaration(
                new DelegationPolicyRegistration(DenyUnlessAuthorizedDelegationPolicy.PolicyId), typeof(DenyUnlessAuthorizedDelegationPolicy)));
        }

        AddJoinStrategy<AllResultsJoinStrategy>(services, GoalJoinStrategyKeys.All);
        AddJoinStrategy<OrdinalFirstSuccessJoinStrategy>(services, GoalJoinStrategyKeys.OrdinalFirstSuccess);
        AddJoinStrategy<FastestValidSuccessJoinStrategy>(services, GoalJoinStrategyKeys.FastestValidSuccess);
        AddJoinStrategy<QuorumJoinStrategy>(services, GoalJoinStrategyKeys.Quorum);
        AddJoinStrategy<BestEffortJoinStrategy>(services, GoalJoinStrategyKeys.BestEffort);
        services.TryAddEnumerable(
        [
            ServiceDescriptor.Singleton<ISessionEntryCodec, GoalCreatedSessionEntryCodec>(),
            ServiceDescriptor.Singleton<ISessionEntryCodec, GoalTransitionSessionEntryCodec>(),
        ]);
        return services;
    }

    internal static IServiceCollection AddGoalProfile(IServiceCollection services, GoalProfileKey key, Action<GoalProfileOptions> configure) =>
        AddProfileContributor(services, key, configure, replace: false);

    internal static IServiceCollection ReplaceGoalProfile(IServiceCollection services, GoalProfileKey key, Action<GoalProfileOptions> configure) =>
        AddProfileContributor(services, key, configure, replace: true);

    internal static IServiceCollection AddGoalStore<TStore>(IServiceCollection services, GoalStoreKey key)
        where TStore : class, IGoalStore =>
        AddKeyed<IGoalStore, TStore>(services, key.Value, nameof(key), replace: false);

    internal static IServiceCollection ReplaceGoalStore<TStore>(IServiceCollection services, GoalStoreKey key)
        where TStore : class, IGoalStore =>
        AddKeyed<IGoalStore, TStore>(services, key.Value, nameof(key), replace: true);

    internal static IServiceCollection AddDelegationTargetProvider<TProvider>(IServiceCollection services)
        where TProvider : class, IDelegationTargetProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentGoals(services, configure: null);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDelegationTargetProvider, TProvider>());
        return services;
    }

    internal static IServiceCollection AddDelegationDispatcher<TDispatcher>(IServiceCollection services, DelegationDispatcherKey key)
        where TDispatcher : class, IDelegationDispatcher =>
        AddKeyed<IDelegationDispatcher, TDispatcher>(services, key.Value, nameof(key), replace: false);

    internal static IServiceCollection AddLocalDelegationDispatcher(IServiceCollection services, DelegationDispatcherKey key)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
        return AddKeyed<IDelegationDispatcher, LocalDelegationDispatcher>(services, key.Value, nameof(key), replace: false);
    }

    internal static IServiceCollection ReplaceDelegationDispatcher<TDispatcher>(IServiceCollection services, DelegationDispatcherKey key)
        where TDispatcher : class, IDelegationDispatcher =>
        AddKeyed<IDelegationDispatcher, TDispatcher>(services, key.Value, nameof(key), replace: true);

    internal static IServiceCollection AddGoalJoinStrategy<TStrategy>(IServiceCollection services, GoalJoinStrategyKey key)
        where TStrategy : class, IGoalJoinStrategy =>
        AddKeyed<IGoalJoinStrategy, TStrategy>(services, key.Value, nameof(key), replace: false);

    internal static IServiceCollection ReplaceGoalJoinStrategy<TStrategy>(IServiceCollection services, GoalJoinStrategyKey key)
        where TStrategy : class, IGoalJoinStrategy =>
        AddKeyed<IGoalJoinStrategy, TStrategy>(services, key.Value, nameof(key), replace: true);

    internal static IServiceCollection AddDelegationPolicy<TPolicy>(IServiceCollection services, DelegationPolicyRegistration registration)
        where TPolicy : class, IDelegationPolicy =>
        AddPolicy<TPolicy>(services, registration, replace: false);

    internal static IServiceCollection ReplaceDelegationPolicy<TPolicy>(IServiceCollection services, DelegationPolicyRegistration registration)
        where TPolicy : class, IDelegationPolicy =>
        AddPolicy<TPolicy>(services, registration, replace: true);

    internal static IServiceCollection AddGoalEventSink<TSink>(IServiceCollection services, GoalEventSinkRegistration registration)
        where TSink : class, IGoalEventSink
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);
        _ = AddAgentGoals(services, configure: null);
        var existing = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(GoalEventSinkDeclaration)
            && descriptor.ImplementationInstance is GoalEventSinkDeclaration declaration
            && declaration.Registration.Id == registration.Id);
        if (existing?.ImplementationInstance is GoalEventSinkDeclaration previous)
        {
            return previous.SinkType == typeof(TSink) && previous.Registration == registration
                ? services
                : throw new InvalidOperationException($"A goal event sink with identity '{registration.Id.Value}' is already registered differently.");
        }

        services.Add(new ServiceDescriptor(typeof(TSink), typeof(TSink), registration.Lifetime));
        return services.AddSingleton(new GoalEventSinkDeclaration(registration, typeof(TSink)));
    }

    internal static IServiceCollection AddSessionBackedGoalStore(
        IServiceCollection services,
        GoalStoreKey key,
        SessionProfileSnapshot profile,
        Action<SessionBackedGoalStoreOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(profile);
        var options = new SessionBackedGoalStoreOptions();
        configure?.Invoke(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaximumAppendAttempts, nameof(configure));
        _ = AddAgentGoals(services, configure: null);
        services.TryAddKeyedSingleton<IGoalStore>(key.Value, (provider, _) => new SessionBackedGoalStore(
            provider.GetRequiredService<ISessionCoordinator>(),
            profile,
            provider.GetRequiredService<ISecurityGrantStore>(),
            provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
            provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
            provider.GetRequiredService<TimeProvider>(),
            options,
            provider.GetService<ILogger<SessionBackedGoalStore>>()));
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>, GuidSessionEntryIdGenerator>();
        return services;
    }

    internal static IServiceCollection ReplaceGoalCoordinator<TCoordinator>(IServiceCollection services)
        where TCoordinator : class, IGoalCoordinator =>
        ReplaceSingular<IGoalCoordinator, TCoordinator>(services);

    internal static IServiceCollection ReplaceGoalStoreSelector<TSelector>(IServiceCollection services)
        where TSelector : class, IGoalStoreSelector =>
        ReplaceSingular<IGoalStoreSelector, TSelector>(services);

    internal static IServiceCollection ReplaceDelegationTargetCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IDelegationTargetCatalog =>
        ReplaceSingular<IDelegationTargetCatalog, TCatalog>(services);

    internal static IServiceCollection ReplaceDelegationTargetSelector<TSelector>(IServiceCollection services)
        where TSelector : class, IDelegationTargetSelector =>
        ReplaceSingular<IDelegationTargetSelector, TSelector>(services);

    internal static IServiceCollection ReplaceDelegationDispatcherSelector<TSelector>(IServiceCollection services)
        where TSelector : class, IDelegationDispatcherSelector =>
        ReplaceSingular<IDelegationDispatcherSelector, TSelector>(services);

    internal static IServiceCollection ReplaceGoalJoinStrategySelector<TSelector>(IServiceCollection services)
        where TSelector : class, IGoalJoinStrategySelector =>
        ReplaceSingular<IGoalJoinStrategySelector, TSelector>(services);

    internal static IServiceCollection ReplaceDelegationPolicyPipeline<TPipeline>(IServiceCollection services)
        where TPipeline : class, IDelegationPolicyPipeline =>
        ReplaceSingular<IDelegationPolicyPipeline, TPipeline>(services);

    internal static IServiceCollection ReplaceDelegationCoordinator<TCoordinator>(IServiceCollection services)
        where TCoordinator : class, IDelegationCoordinator =>
        ReplaceSingular<IDelegationCoordinator, TCoordinator>(services);

    internal static IServiceCollection ReplaceGoalBudgetManager<TManager>(IServiceCollection services)
        where TManager : class, IGoalBudgetManager =>
        ReplaceSingular<IGoalBudgetManager, TManager>(services);

    internal static IServiceCollection ReplaceGoalEventDispatcher<TDispatcher>(IServiceCollection services)
        where TDispatcher : class, IGoalEventDispatcher =>
        ReplaceSingular<IGoalEventDispatcher, TDispatcher>(services);

    internal static IServiceCollection ReplaceDelegationIntentSignal<TSignal>(IServiceCollection services)
        where TSignal : class, IDelegationIntentSignal =>
        ReplaceSingular<IDelegationIntentSignal, TSignal>(services);

    internal static IServiceCollection ReplaceDelegationWaitParking<TParking>(IServiceCollection services)
        where TParking : class, IDelegationWaitParking =>
        ReplaceSingular<IDelegationWaitParking, TParking>(services);

    private static void AddJoinStrategy<TStrategy>(IServiceCollection services, GoalJoinStrategyKey key)
        where TStrategy : class, IGoalJoinStrategy
    {
        if (!services.Any(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(IGoalJoinStrategy) && Equals(descriptor.ServiceKey, key.Value)))
        {
            _ = services.AddKeyedSingleton<IGoalJoinStrategy, TStrategy>(key.Value);
        }
    }

    private static IServiceCollection ReplaceSingular<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = AddAgentGoals(services, configure: null);
        return services.Replace(ServiceDescriptor.Singleton<TService, TImplementation>());
    }

    private static IServiceCollection AddKeyed<TService, TImplementation>(IServiceCollection services, string? key, string parameterName, bool replace)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key, parameterName);
        _ = AddAgentGoals(services, configure: null);
        var existing = services.Where(descriptor => descriptor.IsKeyedService && descriptor.ServiceType == typeof(TService) && Equals(descriptor.ServiceKey, key)).ToList();
        if (replace)
        {
            foreach (var descriptor in existing)
            {
                _ = services.Remove(descriptor);
            }
        }
        else if (existing.Count > 0)
        {
            return existing.All(descriptor => descriptor.KeyedImplementationType == typeof(TImplementation))
                ? services
                : throw new InvalidOperationException($"A {typeof(TService).Name} with key '{key}' is already registered; use the matching Replace method to change it.");
        }

        return services.AddKeyedSingleton<TService, TImplementation>(key);
    }

    private static IServiceCollection AddPolicy<TPolicy>(IServiceCollection services, DelegationPolicyRegistration registration, bool replace)
        where TPolicy : class, IDelegationPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);
        _ = AddAgentGoals(services, configure: null);
        var existing = services.FirstOrDefault(descriptor =>
            descriptor.ServiceType == typeof(DelegationPolicyDeclaration)
            && descriptor.ImplementationInstance is DelegationPolicyDeclaration declaration
            && declaration.Registration.Id == registration.Id);
        if (existing is not null)
        {
            var previous = (DelegationPolicyDeclaration) existing.ImplementationInstance!;
            if (!replace)
            {
                return previous.PolicyType == typeof(TPolicy) && previous.Registration == registration
                    ? services
                    : throw new InvalidOperationException($"A delegation policy with identity '{registration.Id.Value}' is already registered; use ReplaceDelegationPolicy to change it.");
            }

            _ = services.Remove(existing);
        }

        services.TryAddSingleton<TPolicy>();
        return services.AddSingleton(new DelegationPolicyDeclaration(registration, typeof(TPolicy)));
    }

    private static IServiceCollection AddProfileContributor(
        IServiceCollection services,
        GoalProfileKey key,
        Action<GoalProfileOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddAgentGoals(services, configure: null);
        return services.AddSingleton<IGoalProfileContributor>(new GoalProfileContributor(key, configure, replace));
    }
}
