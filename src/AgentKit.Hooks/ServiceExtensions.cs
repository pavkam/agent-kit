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
        public IServiceCollection AddAgentHooks(Action<AgentHookOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddAgentHooks(services, configure);
        }

        /// <summary>Registers one <see cref="IRunStartedHook"/> additively for the <see cref="AgentHookPoints.RunStarted"/> point.</summary>
        /// <typeparam name="THook">The hook implementation; it must be safe to share as a singleton.</typeparam>
        /// <param name="descriptor">The registration descriptor for this hook.</param>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        public IServiceCollection AddRunStartedHook<THook>(HookRegistrationDescriptor descriptor)
            where THook : class, IRunStartedHook
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddRunStartedHook<THook>(services, descriptor);
        }

        /// <summary>Registers one <see cref="IBeforeModelRequestHook"/> additively for the <see cref="AgentHookPoints.BeforeModelRequest"/> point.</summary>
        /// <typeparam name="THook">The hook implementation; it must be safe to share as a singleton.</typeparam>
        /// <param name="descriptor">The registration descriptor for this hook.</param>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        public IServiceCollection AddBeforeModelRequestHook<THook>(HookRegistrationDescriptor descriptor)
            where THook : class, IBeforeModelRequestHook
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddBeforeModelRequestHook<THook>(services, descriptor);
        }

        /// <summary>Registers one <see cref="IBeforeToolInvocationHook"/> additively for the <see cref="AgentHookPoints.BeforeToolInvocation"/> point.</summary>
        /// <typeparam name="THook">The hook implementation; it must be safe to share as a singleton.</typeparam>
        /// <param name="descriptor">The registration descriptor for this hook.</param>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="descriptor"/> is null.</exception>
        public IServiceCollection AddBeforeToolInvocationHook<THook>(HookRegistrationDescriptor descriptor)
            where THook : class, IBeforeToolInvocationHook
        {
            ArgumentNullException.ThrowIfNull(services);
            return HookServiceRegistration.AddBeforeToolInvocationHook<THook>(services, descriptor);
        }

        /// <summary>Replaces the singular <see cref="IHookDispatcher"/> registration.</summary>
        /// <typeparam name="TDispatcher">The replacement dispatcher type.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceHookDispatcher<TDispatcher>()
            where TDispatcher : class, IHookDispatcher
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentHooks(configure: null);
            _ = services.RemoveAll<IHookDispatcher>();
            return services.AddSingleton<IHookDispatcher, TDispatcher>();
        }

        /// <summary>Replaces the singular <see cref="IHookCatalog"/> registration.</summary>
        /// <typeparam name="TCatalog">The replacement catalog type.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceHookCatalog<TCatalog>()
            where TCatalog : class, IHookCatalog
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentHooks(configure: null);
            _ = services.RemoveAll<IHookCatalog>();
            return services.AddSingleton<IHookCatalog, TCatalog>();
        }

        /// <summary>Replaces the singular <see cref="IHookOrderResolver"/> registration.</summary>
        /// <typeparam name="TResolver">The replacement resolver type.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceHookOrderResolver<TResolver>()
            where TResolver : class, IHookOrderResolver
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentHooks(configure: null);
            _ = services.RemoveAll<IHookOrderResolver>();
            return services.AddSingleton<IHookOrderResolver, TResolver>();
        }

        /// <summary>Replaces the singular <see cref="IHookProfileSelector"/> registration.</summary>
        /// <typeparam name="TSelector">The replacement selector type.</typeparam>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
        public IServiceCollection ReplaceHookProfileSelector<TSelector>()
            where TSelector : class, IHookProfileSelector
        {
            ArgumentNullException.ThrowIfNull(services);
            _ = services.AddAgentHooks(configure: null);
            _ = services.RemoveAll<IHookProfileSelector>();
            return services.AddSingleton<IHookProfileSelector, TSelector>();
        }
    }
}
