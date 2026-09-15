// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Dependency-injection registration for the first-party hook dispatcher.</summary>
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="DefaultHookDispatcher"/> as the singular
        /// <see cref="IHookDispatcher"/>, backed by validated
        /// <see cref="AgentHookOptions"/> host ceilings.
        /// </summary>
        /// <param name="configure">
        /// Optional host-ceiling configuration. When null, the documented
        /// <see cref="AgentHookOptions"/> defaults apply and every caller's
        /// requested depth and failure mode is honored unchanged.
        /// </param>
        /// <returns>The same service collection, for chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// The options are validated at startup and again when the dispatcher
        /// is first resolved: <see cref="AgentHookOptions.MaximumInvocationDepth"/>
        /// must be at least 1 and <see cref="AgentHookOptions.MinimumFailureMode"/>
        /// must be a defined value. Invalid values surface as
        /// <c>OptionsValidationException</c> rather than as a mid-run failure.
        /// </para>
        /// <para>
        /// Idempotent for the dispatcher: it uses <c>TryAdd</c> semantics, so
        /// calling this more than once keeps the first dispatcher registration.
        /// Each non-null <paramref name="configure"/> delegate is additive, in
        /// call order, as is standard for the options pattern. This method does
        /// not register any concrete hook implementations; applications and
        /// feature packages register their own hooks additively against
        /// whichever hook interface their point defines.
        /// </para>
        /// </remarks>
        public IServiceCollection AddAgentHooks(Action<AgentHookOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentKitObservability();
            var options = services.AddOptions<AgentHookOptions>()
                .Validate(static value => value.MaximumInvocationDepth >= 1, "MaximumInvocationDepth must be at least 1.")
                .Validate(static value => Enum.IsDefined(value.MinimumFailureMode), "MinimumFailureMode must be a defined value.")
                .ValidateOnStart();
            if (configure is not null)
            {
                _ = options.Configure(configure);
            }

            services.TryAddSingleton<IHookDispatcher, DefaultHookDispatcher>();
            return services;
        }
    }
}
