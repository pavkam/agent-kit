// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO;

/// <summary>Registers first-party input/output coordination components.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers the deterministic first-party policy for selecting already admitted input at active-run safe boundaries.</summary>
        /// <remarks>The registration is replaceable and does not register a queue or imply durable session storage.</remarks>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddInputPromotionPolicy()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IInputPromotionPolicy, DefaultInputPromotionPolicy>();
            return services;
        }

        /// <summary>Registers the first-party input coordinator over an application-selected durable input queue.</summary>
        /// <remarks>
        /// <para>
        /// The registration is replaceable and selects no queue or storage adapter: the application must register exactly one
        /// <see cref="IInputQueue"/>. It also registers this package's default admission-identity generator and clock through
        /// <c>TryAdd</c>, so a host-supplied replacement for either is preserved.
        /// </para>
        /// <para>
        /// <see cref="InputCoordinatorOptions"/> is bound through the standard options pattern. The coordinator captures the
        /// configuration once from <see cref="IOptions{TOptions}"/> when it is constructed; it does not monitor later changes.
        /// Validation of the bound values runs at host start (<c>ValidateOnStart</c>) and whenever the options value is first
        /// materialized, so an invalid <see cref="InputCoordinatorOptions.MaximumInputParts"/> or default
        /// <see cref="InputCoordinatorOptions.PreprocessingConfigurationVersion"/> fails with
        /// <see cref="OptionsValidationException"/> before any admission is attempted.
        /// </para>
        /// <para>
        /// Repeated calls are additive for configuration and idempotent for services: every non-null <paramref name="configure"/>
        /// delegate is appended and later applied in registration order, together with any host
        /// <c>services.Configure&lt;InputCoordinatorOptions&gt;(...)</c> registered before or after this call, while the
        /// coordinator, clock, and identity generator are registered once.
        /// </para>
        /// <para>
        /// The coordinator is registered scoped, not singleton, because the selected <see cref="IInputQueue"/> may
        /// itself be scoped — for example <see cref="AddSessionBackedInputQueue"/>'s queue is
        /// bound to one run's <see cref="SessionExecutionCapability"/>. A singleton coordinator would capture
        /// whichever queue instance happened to be resolved first and hold it for the lifetime of the process,
        /// silently reusing one run's queue for every later run.
        /// </para>
        /// </remarks>
        /// <param name="configure">An optional delegate that adjusts the coordinator bounds and preprocessing evidence, or <see langword="null"/> to keep this package's documented defaults.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddInputCoordinator(Action<InputCoordinatorOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<InputCoordinatorOptions>()
                .Validate(static value => value.MaximumInputParts >= 1, "MaximumInputParts must be at least 1.")
                .Validate(static value => value.PreprocessingConfigurationVersion != default, "PreprocessingConfigurationVersion must be set.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<AdmissionId>, GuidAdmissionIdGenerator>();
            services.TryAddScoped<IInputCoordinator>(static provider => new DefaultInputCoordinator(
                provider.GetRequiredService<IInputQueue>(),
                provider.GetRequiredService<IIdentifierGenerator<AdmissionId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<InputCoordinatorOptions>>(),
                provider.GetService<ILogger<DefaultInputCoordinator>>()
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultInputCoordinator>.Instance));
            return services;
        }

        /// <summary>Registers the first-party input coordinator using the values of a pre-built <see cref="InputCoordinatorOptions"/> instance.</summary>
        /// <remarks>
        /// This is the pre-options-pattern registration shape, retained for source compatibility. It copies
        /// <see cref="InputCoordinatorOptions.PreprocessingConfigurationVersion"/> and <see cref="InputCoordinatorOptions.MaximumInputParts"/>
        /// from <paramref name="options"/> into a configure delegate and forwards to the
        /// <c>AddInputCoordinator(Action&lt;InputCoordinatorOptions&gt;?)</c> overload, so the instance itself is not registered and later
        /// mutation of it has no effect. New code should prefer the delegate overload.
        /// </remarks>
        /// <param name="options">The non-null coordinator bounds and preprocessing evidence whose values are applied.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="options"/> is null.</exception>
        public IServiceCollection AddInputCoordinator(InputCoordinatorOptions options)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            var preprocessingConfigurationVersion = options.PreprocessingConfigurationVersion;
            var maximumInputParts = options.MaximumInputParts;
            return services.AddInputCoordinator(value =>
            {
                value.PreprocessingConfigurationVersion = preprocessingConfigurationVersion;
                value.MaximumInputParts = maximumInputParts;
            });
        }

        /// <summary>Registers the first-party session-backed <see cref="IInputQueue"/> for the currently compiled run scope.</summary>
        /// <remarks>
        /// <para>
        /// <see cref="SessionBackedInputQueue"/> is bound to one run's compiled <see cref="SessionExecutionCapability"/>
        /// and is therefore scoped, not singleton: it must be resolved from a scope that also resolves the exact
        /// <see cref="SessionExecutionCapability"/> for that run. No first-party engine composition registers
        /// <see cref="SessionExecutionCapability"/> as a resolvable service today; a host that calls this method must
        /// register it in the same scope (for example, from a compiled per-run <c>AgentRunPlan</c>) before resolving
        /// <see cref="IInputQueue"/>, or resolution fails with <see cref="InvalidOperationException"/>.
        /// </para>
        /// <para>
        /// <see cref="AgentIOOptions"/> is bound through the standard options pattern; this registration does not
        /// validate it beyond the type's own property defaults, since every bound value already has a safe default.
        /// </para>
        /// </remarks>
        /// <param name="configure">An optional delegate that adjusts queue bounds, or <see langword="null"/> to keep this package's documented defaults.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddSessionBackedInputQueue(Action<AgentIOOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            var options = services.AddOptions<AgentIOOptions>();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IIdentifierGenerator<SessionEntryId>, GuidSessionEntryIdGenerator>();
            services.TryAddSingleton<IIdentifierGenerator<MessageId>, GuidMessageIdGenerator>();
            services.TryAddScoped<IInputQueue>(static provider => new SessionBackedInputQueue(
                provider.GetRequiredService<ISessionCoordinator>(),
                provider.GetRequiredService<SessionExecutionCapability>(),
                provider.GetRequiredService<IIdentifierGenerator<SessionEntryId>>(),
                provider.GetRequiredService<IIdentifierGenerator<MessageId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<IOptions<AgentIOOptions>>()));
            return services;
        }

        /// <summary>Registers the built-in <see cref="DefaultInputCoordinator"/> and <see cref="DefaultOutputPublisher"/> as a keyed, scoped pair.</summary>
        /// <param name="inputKey">The stable key this input coordinator registration is selected by.</param>
        /// <param name="outputKey">The stable key this output publisher registration is selected by.</param>
        /// <param name="configure">Optional configuration for <see cref="AgentIOOptions"/>.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="inputKey"/> or <paramref name="outputKey"/> is uninitialized.</exception>
        /// <exception cref="InvalidOperationException">
        /// A different <see cref="IInputCoordinator"/> or <see cref="IOutputPublisher"/> implementation is already registered under the matching key.
        /// </exception>
        /// <remarks>
        /// Uses <c>TryAddKeyedScoped</c> semantics: repeated equivalent calls for the same keys are idempotent. This
        /// still requires the application to separately select exactly one <see cref="IInputQueue"/> — for example
        /// through <see cref="AddSessionBackedInputQueue"/> — since <see cref="DefaultInputCoordinator"/> coordinates
        /// over an application-selected queue rather than selecting one itself.
        /// </remarks>
        public IServiceCollection AddAgentIO(
            ComponentKey<IInputCoordinator> inputKey,
            ComponentKey<IOutputPublisher> outputKey,
            Action<AgentIOOptions>? configure = null) =>
            AgentIORegistration.Add(services, inputKey, outputKey, configure);

        /// <summary>Additively registers a custom keyed, scoped <see cref="IInputCoordinator"/> implementation.</summary>
        /// <typeparam name="TCoordinator">The scoped coordinator implementation.</typeparam>
        /// <param name="key">The stable key this registration is selected by.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        /// <exception cref="InvalidOperationException">A different <see cref="IInputCoordinator"/> implementation is already registered under <paramref name="key"/>.</exception>
        public IServiceCollection AddInputCoordinator<TCoordinator>(ComponentKey<IInputCoordinator> key)
            where TCoordinator : class, IInputCoordinator =>
            AgentIORegistration.AddInput<TCoordinator>(services, key);

        /// <summary>Replaces whatever <see cref="IInputCoordinator"/> is registered under a key with a new implementation.</summary>
        /// <typeparam name="TCoordinator">The scoped replacement implementation.</typeparam>
        /// <param name="key">The stable key whose registration is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        public IServiceCollection ReplaceInputCoordinator<TCoordinator>(ComponentKey<IInputCoordinator> key)
            where TCoordinator : class, IInputCoordinator =>
            AgentIORegistration.ReplaceInput<TCoordinator>(services, key);

        /// <summary>Additively registers a custom keyed, scoped <see cref="IOutputPublisher"/> implementation.</summary>
        /// <typeparam name="TPublisher">The scoped publisher implementation.</typeparam>
        /// <param name="key">The stable key this registration is selected by.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        /// <exception cref="InvalidOperationException">A different <see cref="IOutputPublisher"/> implementation is already registered under <paramref name="key"/>.</exception>
        public IServiceCollection AddOutputPublisher<TPublisher>(ComponentKey<IOutputPublisher> key)
            where TPublisher : class, IOutputPublisher =>
            AgentIORegistration.AddOutput<TPublisher>(services, key);

        /// <summary>Replaces whatever <see cref="IOutputPublisher"/> is registered under a key with a new implementation.</summary>
        /// <typeparam name="TPublisher">The scoped replacement implementation.</typeparam>
        /// <param name="key">The stable key whose registration is replaced.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> is uninitialized.</exception>
        public IServiceCollection ReplaceOutputPublisher<TPublisher>(ComponentKey<IOutputPublisher> key)
            where TPublisher : class, IOutputPublisher =>
            AgentIORegistration.ReplaceOutput<TPublisher>(services, key);

        /// <summary>Additively registers one <see cref="IRunEventSink"/> under its declared stable name.</summary>
        /// <typeparam name="TSink">The sink implementation, resolved from the service provider when registered there, or constructed otherwise.</typeparam>
        /// <param name="registration">The sink's stable identity, delivery requirement, and fan-out order.</param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="registration"/> is null.</exception>
        /// <exception cref="InvalidOperationException">A different sink registration or implementation type already uses <see cref="RunEventSinkRegistration.SinkName"/>.</exception>
        public IServiceCollection AddRunEventSink<TSink>(RunEventSinkRegistration registration)
            where TSink : class, IRunEventSink =>
            AgentIORegistration.AddRunEventSink<TSink>(services, registration);

        /// <summary>Registers the protected human-question broker over an application-provided channel.</summary>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddHumanQuestionBroker()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>, GuidSecurityEnforcementIntentIdGenerator>();
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IHumanQuestionBroker>(static provider => new DefaultHumanQuestionBroker(
                provider.GetRequiredService<ISecurityGrantStore>(),
                provider.GetRequiredService<IHumanQuestionChannel>(),
                provider.GetRequiredService<IIdentifierGenerator<SecurityEnforcementIntentId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetService<ILogger<DefaultHumanQuestionBroker>>()
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultHumanQuestionBroker>.Instance));
            return services;
        }
    }
}
