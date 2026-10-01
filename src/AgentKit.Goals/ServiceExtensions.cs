// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Registers goal, delegation, and join boundaries without selecting a store or dispatcher.</summary>
/// <remarks>
/// Every registration returns the same collection, builds no service provider, and is idempotent for an equivalent repeat.
/// A registration that reuses a key or identity with a different implementation fails immediately; the matching
/// <c>Replace</c> method names the registration it changes. No store, dispatcher, or persistence target is ever registered
/// implicitly: the application selects each explicitly.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the singular goal and delegation services: coordinators, selectors, target catalog and selector, policy pipeline with its baseline policy, budget manager, event dispatcher, wait parking, the first-party join strategies, and the local-agent target provider.</summary>
        /// <param name="configure">Optional host ceilings and defaults.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Idempotent: repeating it leaves the first registration of every singular service and applies further <paramref name="configure"/> callbacks additively. The local target provider needs the engine's <see cref="IAgentDefinitionCatalog"/>, which the AgentKit facade registers.</remarks>
        public IServiceCollection AddAgentGoals(Action<AgentGoalOptions>? configure = null) => GoalServiceRegistration.AddAgentGoals(services, configure);

        /// <summary>Publishes one versioned goal profile, extending an existing configuration of the same key.</summary>
        /// <param name="key">The profile key.</param>
        /// <param name="configure">The configuration callback.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection AddGoalProfile(GoalProfileKey key, Action<GoalProfileOptions> configure) => GoalServiceRegistration.AddGoalProfile(services, key, configure);

        /// <summary>Publishes one versioned goal profile, discarding any earlier configuration of the same key.</summary>
        /// <param name="key">The profile key.</param>
        /// <param name="configure">The configuration callback.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection ReplaceGoalProfile(GoalProfileKey key, Action<GoalProfileOptions> configure) => GoalServiceRegistration.ReplaceGoalProfile(services, key, configure);

        /// <summary>Registers an additional keyed goal store implementation.</summary>
        /// <typeparam name="TStore">The store type, constructed by the container as a singleton.</typeparam>
        /// <param name="key">The key a profile selects it by.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered with a different implementation.</exception>
        public IServiceCollection AddGoalStore<TStore>(GoalStoreKey key)
            where TStore : class, IGoalStore => GoalServiceRegistration.AddGoalStore<TStore>(services, key);

        /// <summary>Replaces the goal store registered under a key.</summary>
        /// <typeparam name="TStore">The replacement store type.</typeparam>
        /// <param name="key">The key whose registration changes.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection ReplaceGoalStore<TStore>(GoalStoreKey key)
            where TStore : class, IGoalStore => GoalServiceRegistration.ReplaceGoalStore<TStore>(services, key);

        /// <summary>Registers the goal store that projects goals over the selected session contracts.</summary>
        /// <param name="key">The key a profile selects it by.</param>
        /// <param name="profile">The session profile the goal entries are written under.</param>
        /// <param name="configure">Optional projection options.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/>, <paramref name="profile"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The configured append-attempt ceiling is not positive.</exception>
        /// <remarks>The projection creates no separate persistence target: it is exactly as durable as the session store the profile names, and it needs an <see cref="ISessionCoordinator"/> and <see cref="ISecurityGrantStore"/>.</remarks>
        public IServiceCollection AddSessionBackedGoalStore(GoalStoreKey key, SessionProfileSnapshot profile, Action<SessionBackedGoalStoreOptions>? configure = null) =>
            GoalServiceRegistration.AddSessionBackedGoalStore(services, key, profile, configure);

        /// <summary>Registers an additional delegation-target provider.</summary>
        /// <typeparam name="TProvider">The provider type, constructed by the container as a singleton.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddDelegationTargetProvider<TProvider>()
            where TProvider : class, IDelegationTargetProvider => GoalServiceRegistration.AddDelegationTargetProvider<TProvider>(services);

        /// <summary>Registers an additional keyed delegation dispatcher.</summary>
        /// <typeparam name="TDispatcher">The dispatcher type, constructed by the container as a singleton.</typeparam>
        /// <param name="key">The key a profile selects it by.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered with a different implementation.</exception>
        public IServiceCollection AddDelegationDispatcher<TDispatcher>(DelegationDispatcherKey key)
            where TDispatcher : class, IDelegationDispatcher => GoalServiceRegistration.AddDelegationDispatcher<TDispatcher>(services, key);

        /// <summary>Replaces the delegation dispatcher registered under a key.</summary>
        /// <typeparam name="TDispatcher">The replacement dispatcher type.</typeparam>
        /// <param name="key">The key whose registration changes.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection ReplaceDelegationDispatcher<TDispatcher>(DelegationDispatcherKey key)
            where TDispatcher : class, IDelegationDispatcher => GoalServiceRegistration.ReplaceDelegationDispatcher<TDispatcher>(services, key);

        /// <summary>Registers the local dispatcher, which commits a durable child-admission intent for a host worker to drain.</summary>
        /// <param name="key">The key a profile selects it by.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered with a different implementation.</exception>
        /// <remarks>The dispatcher never runs a child and never depends on the engine; it needs an <see cref="ISecurityGrantStore"/> to consume its delegation grant. Pair it with the hosting worker to execute the children it commits.</remarks>
        public IServiceCollection AddLocalDelegationDispatcher(DelegationDispatcherKey key) => GoalServiceRegistration.AddLocalDelegationDispatcher(services, key);

        /// <summary>Registers an additional keyed join strategy.</summary>
        /// <typeparam name="TStrategy">The strategy type, constructed by the container as a singleton.</typeparam>
        /// <param name="key">The key a profile allows and a request declares.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        /// <exception cref="InvalidOperationException">The key is already registered with a different implementation.</exception>
        public IServiceCollection AddGoalJoinStrategy<TStrategy>(GoalJoinStrategyKey key)
            where TStrategy : class, IGoalJoinStrategy => GoalServiceRegistration.AddGoalJoinStrategy<TStrategy>(services, key);

        /// <summary>Replaces the join strategy registered under a key, including a first-party one.</summary>
        /// <typeparam name="TStrategy">The replacement strategy type.</typeparam>
        /// <param name="key">The key whose registration changes.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is blank.</exception>
        public IServiceCollection ReplaceGoalJoinStrategy<TStrategy>(GoalJoinStrategyKey key)
            where TStrategy : class, IGoalJoinStrategy => GoalServiceRegistration.ReplaceGoalJoinStrategy<TStrategy>(services, key);

        /// <summary>Registers an additional delegation policy.</summary>
        /// <typeparam name="TPolicy">The policy type, constructed by the container as a singleton.</typeparam>
        /// <param name="registration">The policy's stable identity and ordering constraints.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The identity is already registered differently.</exception>
        public IServiceCollection AddDelegationPolicy<TPolicy>(DelegationPolicyRegistration registration)
            where TPolicy : class, IDelegationPolicy => GoalServiceRegistration.AddDelegationPolicy<TPolicy>(services, registration);

        /// <summary>Replaces the delegation policy registered under an identity.</summary>
        /// <typeparam name="TPolicy">The replacement policy type.</typeparam>
        /// <param name="registration">The policy's stable identity and ordering constraints.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
        public IServiceCollection ReplaceDelegationPolicy<TPolicy>(DelegationPolicyRegistration registration)
            where TPolicy : class, IDelegationPolicy => GoalServiceRegistration.ReplaceDelegationPolicy<TPolicy>(services, registration);

        /// <summary>Registers an additional goal-event sink.</summary>
        /// <typeparam name="TSink">The sink type, constructed by the container with the registration's lifetime.</typeparam>
        /// <param name="registration">The sink's identity, order, delivery guarantee, lifetime, and profile scope.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
        /// <exception cref="InvalidOperationException">The identity is already registered differently.</exception>
        public IServiceCollection AddGoalEventSink<TSink>(GoalEventSinkRegistration registration)
            where TSink : class, IGoalEventSink => GoalServiceRegistration.AddGoalEventSink<TSink>(services, registration);

        /// <summary>Replaces the singular goal coordinator.</summary>
        /// <typeparam name="TCoordinator">The replacement coordinator type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceGoalCoordinator<TCoordinator>()
            where TCoordinator : class, IGoalCoordinator => GoalServiceRegistration.ReplaceGoalCoordinator<TCoordinator>(services);

        /// <summary>Replaces the singular goal-store selector.</summary>
        /// <typeparam name="TSelector">The replacement selector type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceGoalStoreSelector<TSelector>()
            where TSelector : class, IGoalStoreSelector => GoalServiceRegistration.ReplaceGoalStoreSelector<TSelector>(services);

        /// <summary>Replaces the singular delegation-target catalog.</summary>
        /// <typeparam name="TCatalog">The replacement catalog type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationTargetCatalog<TCatalog>()
            where TCatalog : class, IDelegationTargetCatalog => GoalServiceRegistration.ReplaceDelegationTargetCatalog<TCatalog>(services);

        /// <summary>Replaces the singular delegation-target selector.</summary>
        /// <typeparam name="TSelector">The replacement selector type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationTargetSelector<TSelector>()
            where TSelector : class, IDelegationTargetSelector => GoalServiceRegistration.ReplaceDelegationTargetSelector<TSelector>(services);

        /// <summary>Replaces the singular delegation-dispatcher selector.</summary>
        /// <typeparam name="TSelector">The replacement selector type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationDispatcherSelector<TSelector>()
            where TSelector : class, IDelegationDispatcherSelector => GoalServiceRegistration.ReplaceDelegationDispatcherSelector<TSelector>(services);

        /// <summary>Replaces the singular join-strategy selector.</summary>
        /// <typeparam name="TSelector">The replacement selector type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceGoalJoinStrategySelector<TSelector>()
            where TSelector : class, IGoalJoinStrategySelector => GoalServiceRegistration.ReplaceGoalJoinStrategySelector<TSelector>(services);

        /// <summary>Replaces the singular delegation-policy pipeline.</summary>
        /// <typeparam name="TPipeline">The replacement pipeline type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationPolicyPipeline<TPipeline>()
            where TPipeline : class, IDelegationPolicyPipeline => GoalServiceRegistration.ReplaceDelegationPolicyPipeline<TPipeline>(services);

        /// <summary>Replaces the singular delegation coordinator.</summary>
        /// <typeparam name="TCoordinator">The replacement coordinator type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationCoordinator<TCoordinator>()
            where TCoordinator : class, IDelegationCoordinator => GoalServiceRegistration.ReplaceDelegationCoordinator<TCoordinator>(services);

        /// <summary>Replaces the singular goal budget manager.</summary>
        /// <typeparam name="TManager">The replacement manager type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceGoalBudgetManager<TManager>()
            where TManager : class, IGoalBudgetManager => GoalServiceRegistration.ReplaceGoalBudgetManager<TManager>(services);

        /// <summary>Replaces the singular goal-event dispatcher.</summary>
        /// <typeparam name="TDispatcher">The replacement dispatcher type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceGoalEventDispatcher<TDispatcher>()
            where TDispatcher : class, IGoalEventDispatcher => GoalServiceRegistration.ReplaceGoalEventDispatcher<TDispatcher>(services);

        /// <summary>Replaces the singular intent signal that wakes a host worker when a child-admission intent commits.</summary>
        /// <typeparam name="TSignal">The replacement signal type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationIntentSignal<TSignal>()
            where TSignal : class, IDelegationIntentSignal => GoalServiceRegistration.ReplaceDelegationIntentSignal<TSignal>(services);

        /// <summary>Replaces the singular wait parking that releases a waiting run's worker occupancy.</summary>
        /// <typeparam name="TParking">The replacement parking type.</typeparam>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceDelegationWaitParking<TParking>()
            where TParking : class, IDelegationWaitParking => GoalServiceRegistration.ReplaceDelegationWaitParking<TParking>(services);
    }
}
