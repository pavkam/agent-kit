// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

using Microsoft.Extensions.DependencyInjection;

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
            return HookServiceRegistration.AddAgentHooks(services, configure);
        }

        /// <summary>Registers one <see cref="IRunStartedHook"/> additively for the <see cref="AgentHookPoints.RunStarted"/> point.</summary>
        /// <typeparam name="THook">The hook implementation; it must be safe to share as a singleton.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>
        /// Registrations are additive and de-duplicated per implementation type. <see cref="AddAgentHooks"/> is also
        /// registered so the loop has a dispatcher; a loop that finds hooks without a dispatcher fails closed.
        /// </remarks>
        public IServiceCollection AddRunStartedHook<THook>()
            where THook : class, IRunStartedHook
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddRunStartedHook<THook>(services);
        }

        /// <summary>Registers one <see cref="IBeforeModelRequestHook"/> additively for the <see cref="AgentHookPoints.BeforeModelRequest"/> point.</summary>
        /// <typeparam name="THook">The hook implementation; it must be safe to share as a singleton.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Registrations are additive and de-duplicated per implementation type; <see cref="AddAgentHooks"/> is also registered.</remarks>
        public IServiceCollection AddBeforeModelRequestHook<THook>()
            where THook : class, IBeforeModelRequestHook
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddBeforeModelRequestHook<THook>(services);
        }

        /// <summary>Registers one <see cref="IBeforeToolInvocationHook"/> additively for the <see cref="AgentHookPoints.BeforeToolInvocation"/> point.</summary>
        /// <typeparam name="THook">The hook implementation; it must be safe to share as a singleton.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        /// <remarks>Registrations are additive and de-duplicated per implementation type; <see cref="AddAgentHooks"/> is also registered.</remarks>
        public IServiceCollection AddBeforeToolInvocationHook<THook>()
            where THook : class, IBeforeToolInvocationHook
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddBeforeToolInvocationHook<THook>(services);
        }
    }
}
