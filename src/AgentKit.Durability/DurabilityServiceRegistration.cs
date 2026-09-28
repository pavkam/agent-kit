// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Package-internal dependency-injection registration for the durability runtime.</summary>
/// <remarks>
/// Every method mutates the supplied collection only. None builds or resolves a service provider, and none registers a
/// backend, journal, lease manager, or any other persistence target: applications select those explicitly. Engine-wide
/// singletons use <c>TryAdd</c> so repeated calls are idempotent, while keyed components are additive with explicit
/// per-key replacement.
/// </remarks>
internal static class DurabilityServiceRegistration
{
    /// <summary>Registers the provider-neutral durability coordinator, catalogs, selectors, and generators.</summary>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="configure">An optional callback applied to the engine-wide durability options.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddAgentDurability(IServiceCollection services, Action<AgentDurabilityOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.AddAgentKitObservability();
        var optionsBuilder = services.AddOptions<AgentDurabilityOptions>()
            .Validate(static options => options.LeaseDuration > TimeSpan.Zero, "LeaseDuration must be positive.")
            .Validate(static options => options.LeaseRenewalInterval > TimeSpan.Zero, "LeaseRenewalInterval must be positive.")
            .Validate(
                static options => options.LeaseRenewalInterval < options.LeaseDuration,
                "LeaseRenewalInterval must be shorter than LeaseDuration so ownership can be renewed before it expires.")
            .Validate(static options => options.MaximumRecoveryAttempts > 0, "MaximumRecoveryAttempts must be positive.")
            .ValidateOnStart();
        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton(static provider =>
        {
            var registry = new DurabilityProfileRegistry();
            foreach (var contributor in provider.GetServices<IDurabilityProfileContributor>())
            {
                contributor.Contribute(registry);
            }

            return registry;
        });
        services.TryAddSingleton<IDurabilityProfileCatalog, InMemoryDurabilityProfileCatalog>();
        services.TryAddSingleton<IDurableBackendCatalog, DurableBackendCatalog>();
        services.TryAddSingleton<IDurableBackendSelector, DefaultDurableBackendSelector>();
        services.TryAddSingleton<IDurabilityRuntimeSelector, DefaultDurabilityRuntimeSelector>();
        services.TryAddSingleton<IDurableExecutionEventDispatcher, DefaultDurableExecutionEventDispatcher>();
        services.TryAddSingleton<IDurableExecutionCoordinator, DurableExecutionCoordinator>();
        services.TryAddSingleton<DurableBoundaryRegistry>();
        services.TryAddSingleton<IIdentifierGenerator<CheckpointId>, GuidCheckpointIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<WorkerId>, GuidWorkerIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityRequestId>, GuidSecurityRequestIdGenerator>();
        services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>Additively registers one named durability profile.</summary>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault profile key.</param>
    /// <param name="configure">The configuration callback applied to the profile options.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection AddDurabilityProfile(
        IServiceCollection services,
        DurabilityProfileKey key,
        Action<DurabilityProfileOptions> configure) =>
        AddProfileContributor(services, key, configure, replace: false);

    /// <summary>Replaces any previously accumulated configuration for one named durability profile.</summary>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault profile key.</param>
    /// <param name="configure">The configuration callback applied to fresh profile options.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection ReplaceDurabilityProfile(
        IServiceCollection services,
        DurabilityProfileKey key,
        Action<DurabilityProfileOptions> configure) =>
        AddProfileContributor(services, key, configure, replace: true);

    /// <summary>Additively registers one durable backend under its key.</summary>
    /// <typeparam name="TBackend">The concrete backend implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault backend key.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection AddDurabilityBackend<TBackend>(IServiceCollection services, DurableBackendKey key)
        where TBackend : class, IDurableExecutionBackend
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return services.AddKeyedSingleton<IDurableExecutionBackend, TBackend>(key.Value);
    }

    /// <summary>Replaces the durable backend registered under one key.</summary>
    /// <typeparam name="TBackend">The concrete backend implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault backend key whose registrations are replaced.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection ReplaceDurabilityBackend<TBackend>(IServiceCollection services, DurableBackendKey key)
        where TBackend : class, IDurableExecutionBackend
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IDurableExecutionBackend>(services, key.Value);
        return services.AddKeyedSingleton<IDurableExecutionBackend, TBackend>(key.Value);
    }

    /// <summary>Additively registers one durable operation journal under its key.</summary>
    /// <typeparam name="TJournal">The concrete journal implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault journal key.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection AddJournal<TJournal>(IServiceCollection services, DurableJournalKey key)
        where TJournal : class, IDurableOperationJournal
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return services.AddKeyedSingleton<IDurableOperationJournal, TJournal>(key.Value);
    }

    /// <summary>Replaces the durable operation journal registered under one key.</summary>
    /// <typeparam name="TJournal">The concrete journal implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault journal key whose registrations are replaced.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection ReplaceJournal<TJournal>(IServiceCollection services, DurableJournalKey key)
        where TJournal : class, IDurableOperationJournal
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IDurableOperationJournal>(services, key.Value);
        return services.AddKeyedSingleton<IDurableOperationJournal, TJournal>(key.Value);
    }

    /// <summary>Additively registers one durable lease manager under its key.</summary>
    /// <typeparam name="TLeaseManager">The concrete lease-manager implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault lease-manager key.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection AddLeaseManager<TLeaseManager>(IServiceCollection services, DurableLeaseManagerKey key)
        where TLeaseManager : class, IDurableLeaseManager
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return services.AddKeyedSingleton<IDurableLeaseManager, TLeaseManager>(key.Value);
    }

    /// <summary>Replaces the durable lease manager registered under one key.</summary>
    /// <typeparam name="TLeaseManager">The concrete lease-manager implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault lease-manager key whose registrations are replaced.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection ReplaceLeaseManager<TLeaseManager>(IServiceCollection services, DurableLeaseManagerKey key)
        where TLeaseManager : class, IDurableLeaseManager
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IDurableLeaseManager>(services, key.Value);
        return services.AddKeyedSingleton<IDurableLeaseManager, TLeaseManager>(key.Value);
    }

    /// <summary>Additively registers one recovery policy under its key.</summary>
    /// <typeparam name="TPolicy">The concrete recovery-policy implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault recovery-policy key.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection AddRecoveryPolicy<TPolicy>(IServiceCollection services, RecoveryPolicyKey key)
        where TPolicy : class, IRecoveryPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        return services.AddKeyedSingleton<IRecoveryPolicy, TPolicy>(key.Value);
    }

    /// <summary>Replaces the recovery policy registered under one key.</summary>
    /// <typeparam name="TPolicy">The concrete recovery-policy implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="key">The nondefault recovery-policy key whose registrations are replaced.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
    internal static IServiceCollection ReplaceRecoveryPolicy<TPolicy>(IServiceCollection services, RecoveryPolicyKey key)
        where TPolicy : class, IRecoveryPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        RemoveKeyed<IRecoveryPolicy>(services, key.Value);
        return services.AddKeyedSingleton<IRecoveryPolicy, TPolicy>(key.Value);
    }

    /// <summary>Additively registers one durable operation state codec.</summary>
    /// <typeparam name="TState">The durable operation state the codec encodes and decodes.</typeparam>
    /// <typeparam name="TCodec">The concrete codec implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddDurableOperationCodec<TState, TCodec>(IServiceCollection services)
        where TCodec : class, IDurableOperationCodec<TState>
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IDurableOperationCodec<TState>, TCodec>();
    }

    /// <summary>Replaces every registered codec for one durable operation state.</summary>
    /// <typeparam name="TState">The durable operation state the codec encodes and decodes.</typeparam>
    /// <typeparam name="TCodec">The concrete codec implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceDurableOperationCodec<TState, TCodec>(IServiceCollection services)
        where TCodec : class, IDurableOperationCodec<TState>
    {
        ArgumentNullException.ThrowIfNull(services);
        _ = services.RemoveAll<IDurableOperationCodec<TState>>();
        return services.AddSingleton<IDurableOperationCodec<TState>, TCodec>();
    }

    /// <summary>Additively registers one ordered durable execution event sink.</summary>
    /// <typeparam name="TSink">The concrete sink implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <param name="registration">The non-null declared dispatch metadata.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
    internal static IServiceCollection AddEventSink<TSink>(
        IServiceCollection services,
        DurableExecutionEventSinkRegistration registration)
        where TSink : class, IDurableExecutionEventSink
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registration);
        _ = AddAgentDurability(services, configure: null);
        services.Add(new ServiceDescriptor(typeof(TSink), typeof(TSink), registration.Lifetime));
        return services.AddSingleton(new DurableExecutionEventSinkDeclaration(registration, typeof(TSink)));
    }

    /// <summary>Replaces the engine-wide durability runtime selector.</summary>
    /// <typeparam name="TSelector">The concrete runtime-selector implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceRuntimeSelector<TSelector>(IServiceCollection services)
        where TSelector : class, IDurabilityRuntimeSelector
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Replace(ServiceDescriptor.Singleton<IDurabilityRuntimeSelector, TSelector>());
    }

    /// <summary>Replaces the engine-wide durable execution coordinator.</summary>
    /// <typeparam name="TCoordinator">The concrete coordinator implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceCoordinator<TCoordinator>(IServiceCollection services)
        where TCoordinator : class, IDurableExecutionCoordinator
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Replace(ServiceDescriptor.Singleton<IDurableExecutionCoordinator, TCoordinator>());
    }

    /// <summary>Replaces the engine-wide durable backend catalog.</summary>
    /// <typeparam name="TCatalog">The concrete catalog implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceBackendCatalog<TCatalog>(IServiceCollection services)
        where TCatalog : class, IDurableBackendCatalog
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Replace(ServiceDescriptor.Singleton<IDurableBackendCatalog, TCatalog>());
    }

    /// <summary>Replaces the engine-wide durable backend selector.</summary>
    /// <typeparam name="TSelector">The concrete selector implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceBackendSelector<TSelector>(IServiceCollection services)
        where TSelector : class, IDurableBackendSelector
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Replace(ServiceDescriptor.Singleton<IDurableBackendSelector, TSelector>());
    }

    /// <summary>Replaces the engine-wide durable execution event dispatcher.</summary>
    /// <typeparam name="TDispatcher">The concrete dispatcher implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection ReplaceEventDispatcher<TDispatcher>(IServiceCollection services)
        where TDispatcher : class, IDurableExecutionEventDispatcher
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.Replace(ServiceDescriptor.Singleton<IDurableExecutionEventDispatcher, TDispatcher>());
    }

    /// <summary>Additively registers one durable operation handler.</summary>
    /// <typeparam name="THandler">The concrete handler implementation.</typeparam>
    /// <param name="services">The non-null collection being configured.</param>
    /// <returns>The same collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    internal static IServiceCollection AddHandler<THandler>(IServiceCollection services)
        where THandler : class, IDurableOperationHandler
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IDurableOperationHandler, THandler>();
    }

    private static IServiceCollection AddProfileContributor(
        IServiceCollection services,
        DurabilityProfileKey key,
        Action<DurabilityProfileOptions> configure,
        bool replace)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(configure);
        _ = AddAgentDurability(services, configure: null);
        return services.AddSingleton<IDurabilityProfileContributor>(
            new DurabilityProfileContributor(key, configure, replace));
    }

    private static void RemoveKeyed<TService>(IServiceCollection services, object serviceKey)
    {
        Debug.Assert(services is not null, "Callers validate the collection before removing keyed descriptors.");
        Debug.Assert(serviceKey is not null, "Callers validate the key before removing keyed descriptors.");
        for (var index = services.Count - 1; index >= 0; index--)
        {
            var descriptor = services[index];
            if (descriptor.ServiceType == typeof(TService) && Equals(descriptor.ServiceKey, serviceKey))
            {
                services.RemoveAt(index);
            }
        }
    }
}
