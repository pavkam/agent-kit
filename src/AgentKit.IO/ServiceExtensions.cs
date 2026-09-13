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
        /// The registration is replaceable and selects no queue or storage adapter: the application must register exactly one
        /// <see cref="IInputQueue"/>. It also registers this package's default admission-identity generator, clock, and
        /// coordinator options through <c>TryAdd</c>, so a host-supplied replacement for any of them is preserved.
        /// </remarks>
        /// <param name="options">The coordinator bounds and preprocessing evidence, or <see langword="null"/> to select this package's documented defaults.</param>
        /// <returns>The same service collection for composition chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection AddInputCoordinator(InputCoordinatorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton(options ?? new InputCoordinatorOptions());
            services.TryAddSingleton<IIdentifierGenerator<AdmissionId>, GuidAdmissionIdGenerator>();
            services.TryAddSingleton<IInputCoordinator>(static provider => new DefaultInputCoordinator(
                provider.GetRequiredService<IInputQueue>(),
                provider.GetRequiredService<IIdentifierGenerator<AdmissionId>>(),
                provider.GetRequiredService<TimeProvider>(),
                provider.GetRequiredService<InputCoordinatorOptions>(),
                provider.GetService<ILogger<DefaultInputCoordinator>>()
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DefaultInputCoordinator>.Instance));
            return services;
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
