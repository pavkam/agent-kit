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
            services.TryAddSingleton<IInputCoordinator>(static provider => new DefaultInputCoordinator(
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
