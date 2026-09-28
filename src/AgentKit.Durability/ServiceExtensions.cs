// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for the provider-neutral durability runtime.</summary>
/// <remarks>
/// These methods mutate the supplied collection and never build or resolve a service provider. The package installs no
/// backend, journal, or lease manager: process memory is not durable, so an application selects every persistence
/// target explicitly through the keyed registrations below.
/// </remarks>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the provider-neutral durability coordinator, catalogs, selectors, and identity generators.</summary>
        /// <param name="configure">An optional callback applied to the engine-wide durability options.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>
        /// The call is idempotent: repeated invocations reuse the existing singular coordinator, backend catalog,
        /// backend selector, runtime selector, and event dispatcher, and apply each supplied configuration callback.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection AddAgentDurability(Action<AgentDurabilityOptions>? configure = null) =>
            DurabilityServiceRegistration.AddAgentDurability(services, configure);

        /// <summary>Additively registers one named durability profile.</summary>
        /// <param name="key">The nondefault profile key an agent definition may select.</param>
        /// <param name="configure">The callback that selects this profile's backend, journal, lease manager, recovery policy, and enabled operations.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Repeated registrations for the same key refine the accumulated options in registration order.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection AddDurabilityProfile(DurabilityProfileKey key, Action<DurabilityProfileOptions> configure) =>
            DurabilityServiceRegistration.AddDurabilityProfile(services, key, configure);

        /// <summary>Replaces any configuration accumulated for one named durability profile.</summary>
        /// <param name="key">The nondefault profile key being replaced.</param>
        /// <param name="configure">The callback applied to fresh profile options.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Replacement discards earlier contributions for this key; later additive contributions still refine the result.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection or <paramref name="configure"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection ReplaceDurabilityProfile(DurabilityProfileKey key, Action<DurabilityProfileOptions> configure) =>
            DurabilityServiceRegistration.ReplaceDurabilityProfile(services, key, configure);

        /// <summary>Additively registers one durable execution backend under its key.</summary>
        /// <typeparam name="TBackend">The concrete backend implementation, resolved as a container-owned singleton.</typeparam>
        /// <param name="key">The nondefault backend key a profile and persisted context reference.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Registering the same key twice leaves both descriptors present; resolution uses the last one.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection AddDurabilityBackend<TBackend>(DurableBackendKey key)
            where TBackend : class, IDurableExecutionBackend =>
            DurabilityServiceRegistration.AddDurabilityBackend<TBackend>(services, key);

        /// <summary>Replaces the durable execution backend registered under one key.</summary>
        /// <typeparam name="TBackend">The concrete backend implementation.</typeparam>
        /// <param name="key">The nondefault backend key whose existing registrations are removed first.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Replacement affects later compositions only; it never redirects an already activated runtime lease.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection ReplaceDurabilityBackend<TBackend>(DurableBackendKey key)
            where TBackend : class, IDurableExecutionBackend =>
            DurabilityServiceRegistration.ReplaceDurabilityBackend<TBackend>(services, key);

        /// <summary>Additively registers one durable operation journal under its key.</summary>
        /// <typeparam name="TJournal">The concrete journal implementation, resolved as a container-owned singleton.</typeparam>
        /// <param name="key">The nondefault journal key a profile and persisted context reference.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Registering the same key twice leaves both descriptors present; resolution uses the last one.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection AddDurableJournal<TJournal>(DurableJournalKey key)
            where TJournal : class, IDurableOperationJournal =>
            DurabilityServiceRegistration.AddJournal<TJournal>(services, key);

        /// <summary>Replaces the durable operation journal registered under one key.</summary>
        /// <typeparam name="TJournal">The concrete journal implementation.</typeparam>
        /// <param name="key">The nondefault journal key whose existing registrations are removed first.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>
        /// Replacement affects later compositions only. A decorator must be layered over an explicitly selected
        /// journal; this method never supplies a persistence target of its own.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection ReplaceDurableJournal<TJournal>(DurableJournalKey key)
            where TJournal : class, IDurableOperationJournal =>
            DurabilityServiceRegistration.ReplaceJournal<TJournal>(services, key);

        /// <summary>Additively registers one durable lease manager under its key.</summary>
        /// <typeparam name="TLeaseManager">The concrete lease-manager implementation, resolved as a container-owned singleton.</typeparam>
        /// <param name="key">The nondefault lease-manager key a profile and persisted context reference.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Registering the same key twice leaves both descriptors present; resolution uses the last one.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection AddDurableLeaseManager<TLeaseManager>(DurableLeaseManagerKey key)
            where TLeaseManager : class, IDurableLeaseManager =>
            DurabilityServiceRegistration.AddLeaseManager<TLeaseManager>(services, key);

        /// <summary>Replaces the durable lease manager registered under one key.</summary>
        /// <typeparam name="TLeaseManager">The concrete lease-manager implementation.</typeparam>
        /// <param name="key">The nondefault lease-manager key whose existing registrations are removed first.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Replacement affects later compositions only; an outstanding execution lease keeps its original owner.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection ReplaceDurableLeaseManager<TLeaseManager>(DurableLeaseManagerKey key)
            where TLeaseManager : class, IDurableLeaseManager =>
            DurabilityServiceRegistration.ReplaceLeaseManager<TLeaseManager>(services, key);

        /// <summary>Additively registers one recovery policy under its key.</summary>
        /// <typeparam name="TPolicy">The concrete recovery-policy implementation, resolved as a container-owned singleton.</typeparam>
        /// <param name="key">The nondefault recovery-policy key a profile and persisted context reference.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Registering the same key twice leaves both descriptors present; resolution uses the last one.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection AddRecoveryPolicy<TPolicy>(RecoveryPolicyKey key)
            where TPolicy : class, IRecoveryPolicy =>
            DurabilityServiceRegistration.AddRecoveryPolicy<TPolicy>(services, key);

        /// <summary>Replaces the recovery policy registered under one key.</summary>
        /// <typeparam name="TPolicy">The concrete recovery-policy implementation.</typeparam>
        /// <param name="key">The nondefault recovery-policy key whose existing registrations are removed first.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Replacement affects later compositions only; an in-flight recovery keeps its captured policy.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is default, empty, or whitespace.</exception>
        public IServiceCollection ReplaceRecoveryPolicy<TPolicy>(RecoveryPolicyKey key)
            where TPolicy : class, IRecoveryPolicy =>
            DurabilityServiceRegistration.ReplaceRecoveryPolicy<TPolicy>(services, key);

        /// <summary>Additively registers one durable operation state codec.</summary>
        /// <typeparam name="TState">The durable operation state this codec encodes and decodes.</typeparam>
        /// <typeparam name="TCodec">The concrete codec implementation, resolved as a container-owned singleton.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>A profile must have a codec for every enabled operation version, or composition validation fails.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection AddDurableOperationCodec<TState, TCodec>()
            where TCodec : class, IDurableOperationCodec<TState> =>
            DurabilityServiceRegistration.AddDurableOperationCodec<TState, TCodec>(services);

        /// <summary>Replaces every codec registered for one durable operation state.</summary>
        /// <typeparam name="TState">The durable operation state this codec encodes and decodes.</typeparam>
        /// <typeparam name="TCodec">The concrete codec implementation.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>
        /// Replacement affects later compositions only. Changing a codec does not reinterpret payloads already written
        /// under an earlier operation version; that remains a typed incompatibility at recovery.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection ReplaceDurableOperationCodec<TState, TCodec>()
            where TCodec : class, IDurableOperationCodec<TState> =>
            DurabilityServiceRegistration.ReplaceDurableOperationCodec<TState, TCodec>(services);

        /// <summary>Additively registers one ordered durable execution event sink.</summary>
        /// <typeparam name="TSink">The concrete sink implementation, resolved per publication under the declared lifetime.</typeparam>
        /// <param name="registration">The non-null declared dispatch order, delivery strictness, lifetime, and profile filter.</param>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>
        /// Observational sinks never change a durable outcome. A required sink that is unavailable or fails makes the
        /// publishing operation fail closed, because unobserved durable work must not be reported as observed.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The receiving collection or <paramref name="registration"/> is null.</exception>
        public IServiceCollection AddDurableExecutionEventSink<TSink>(DurableExecutionEventSinkRegistration registration)
            where TSink : class, IDurableExecutionEventSink =>
            DurabilityServiceRegistration.AddEventSink<TSink>(services, registration);

        /// <summary>Replaces the engine-wide durability runtime selector.</summary>
        /// <typeparam name="TSelector">The concrete runtime-selector implementation.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection ReplaceDurabilityRuntimeSelector<TSelector>()
            where TSelector : class, IDurabilityRuntimeSelector =>
            DurabilityServiceRegistration.ReplaceRuntimeSelector<TSelector>(services);

        /// <summary>Replaces the engine-wide durable execution coordinator.</summary>
        /// <typeparam name="TCoordinator">The concrete coordinator implementation.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection ReplaceDurableExecutionCoordinator<TCoordinator>()
            where TCoordinator : class, IDurableExecutionCoordinator =>
            DurabilityServiceRegistration.ReplaceCoordinator<TCoordinator>(services);

        /// <summary>Replaces the engine-wide durable backend catalog.</summary>
        /// <typeparam name="TCatalog">The concrete catalog implementation.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection ReplaceDurableBackendCatalog<TCatalog>()
            where TCatalog : class, IDurableBackendCatalog =>
            DurabilityServiceRegistration.ReplaceBackendCatalog<TCatalog>(services);

        /// <summary>Replaces the engine-wide durable backend selector.</summary>
        /// <typeparam name="TSelector">The concrete selector implementation.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>A replacement selector must still fail closed; backend selection never silently falls back.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection ReplaceDurableBackendSelector<TSelector>()
            where TSelector : class, IDurableBackendSelector =>
            DurabilityServiceRegistration.ReplaceBackendSelector<TSelector>(services);

        /// <summary>Replaces the engine-wide durable execution event dispatcher.</summary>
        /// <typeparam name="TDispatcher">The concrete dispatcher implementation.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection ReplaceDurableExecutionEventDispatcher<TDispatcher>()
            where TDispatcher : class, IDurableExecutionEventDispatcher =>
            DurabilityServiceRegistration.ReplaceEventDispatcher<TDispatcher>(services);

        /// <summary>Additively registers one durable operation handler.</summary>
        /// <typeparam name="THandler">The concrete handler implementation, resolved as a container-owned singleton.</typeparam>
        /// <returns>The same collection for chaining.</returns>
        /// <remarks>Each handler owns exactly one operation name; registering two handlers for one name is a composition error.</remarks>
        /// <exception cref="ArgumentNullException">The receiving collection is null.</exception>
        public IServiceCollection AddDurableOperationHandler<THandler>()
            where THandler : class, IDurableOperationHandler =>
            DurabilityServiceRegistration.AddHandler<THandler>(services);
    }
}
